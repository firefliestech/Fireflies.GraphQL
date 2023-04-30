using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Federation;
using Fireflies.GraphQL.Core.Json;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.IoC.Abstractions;
using Fireflies.Logging.Abstractions;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using System.Threading.Tasks.Dataflow;
using Fireflies.GraphQL.Core.Extensions;
using static Fireflies.GraphQL.Core.OperationVisitor;

namespace Fireflies.GraphQL.Core;

internal class OperationVisitor : ASTVisitor<RequestContext> {
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly FragmentAccessor _fragmentAccessor;
    private readonly ValueAccessor _valueAccessor;
    private readonly OperationType _operationType;
    private readonly RequestContext _context;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly ResultJsonWriter? _writer;
    private readonly WrapperRegistry _wrapperRegistry;

    private static readonly MethodInfo GetResultMethod;
    private readonly IFirefliesLogger _logger;

    static OperationVisitor() {
        GetResultMethod = typeof(OperationVisitor).GetMethod(nameof(GetResult), BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    public OperationVisitor(GraphQLOptions options, IDependencyResolver dependencyResolver, FragmentAccessor fragmentAccessor, ValueAccessor valueAccessor, WrapperRegistry wrapperRegistry, OperationType operationType, RequestContext context, ScalarRegistry scalarRegistry, ResultJsonWriter? writer, IFirefliesLoggerFactory loggerFactory) {
        _options = options;
        _dependencyResolver = dependencyResolver;
        _fragmentAccessor = fragmentAccessor;
        _valueAccessor = valueAccessor;
        _wrapperRegistry = wrapperRegistry;

        _operationType = operationType;
        _context = context;
        _scalarRegistry = scalarRegistry;
        _writer = writer;
        _logger = loggerFactory.GetLogger<OperationVisitor>();
    }

    protected override async ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, RequestContext context) {
        foreach(var graphQLField in selectionSet.Selections.OfType<GraphQLField>()) {
            var operationDescriptor = GetHandler(graphQLField);
            var operations = _dependencyResolver.Resolve(operationDescriptor.Type);

            var returnType = ReflectionCache.GetReturnType(operationDescriptor.Method);
            var executeInParallel = operationDescriptor.Method.HasCustomAttribute<GraphQLParallel>(out var parallelAttribute);
            var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, operationDescriptor.Method, _valueAccessor, _fragmentAccessor, _context, _dependencyResolver, new ResultContext(returnType, context));
            var methodInvoker = ReflectionCache.GetGenericMethodInvoker(GetResultMethod, new[] { returnType }, typeof(OperationDescriptor), typeof(object), typeof(ArgumentBuilder), typeof(GraphQLField));
            try {
                var asyncEnumerable = (IAsyncEnumerable<object?>)methodInvoker(this, operationDescriptor, operations, argumentBuilder, graphQLField);
                await foreach(var result in asyncEnumerable.WithCancellation(context.CancellationToken).ConfigureAwait(false)) {
                    var writer = _writer ?? new ResultJsonWriter(_scalarRegistry);

                    if(operationDescriptor.Method.HasCustomAttribute<GraphQLFederatedAttribute>()) {
                        var fieldName = graphQLField.Alias?.Name.StringValue ?? graphQLField.Name.StringValue;
                        if(result != null) {
                            writer.WriteRaw(fieldName, (JsonNode)result);
                        } else {
                            writer.WriteNull(fieldName);
                        }
                    } else {
                        var fieldName = graphQLField.Alias?.Name.StringValue ?? graphQLField.Name.StringValue;
                        if(result is IEnumerable enumerable) {
                            writer.WriteStartArray(fieldName);

                            if(executeInParallel) {
                                await ExecuteParallel(context, enumerable, writer, graphQLField, parallelAttribute!).ConfigureAwait(false);
                            } else {
                                await ExecuteSynchronously(context, enumerable, writer, graphQLField).ConfigureAwait(false);
                            }

                            writer.WriteEndArray();
                        } else {
                            if(result != null) {
                                writer.WriteStartObject(fieldName);
                                await WriteObject(context, writer, graphQLField, result).ConfigureAwait(false);
                                writer.WriteEndObject();
                            } else {
                                writer.WriteNull(fieldName);
                            }
                        }
                    }

                    await context.PublishResult(writer).ConfigureAwait(false);
                }
            } catch(OperationCanceledException) {
                // Noop
            } catch(FederationExecutionException fex) {
                var writer = _writer ?? new ResultJsonWriter(_scalarRegistry);
                foreach(var error in fex.Node) {
                    var message = error["message"]!.GetValue<string>();
                    var code = error["extensions"]!["code"].GetValue<string>();

                    if(code != "GRAPHQL_VALIDATION_FAILED")
                        _logger.Error($"Error occurred during federated request. Message: {message}. Code: {code}");

                    writer.AddError(message, code);
                }

                await context.PublishResult(writer).ConfigureAwait(false);
            } catch(Exception ex) {
                _logger.Error(ex, "Exception occured while processing request");
                var writer = _writer ?? new ResultJsonWriter(_scalarRegistry);
                writer.AddError("Internal server error occurred", "GRAPHQL_EXECUTION_FAILED");
                await context.PublishResult(writer).ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteSynchronously(RequestContext context, IEnumerable enumerable, ResultJsonWriter writer, GraphQLField graphQLField) {
        foreach(var obj in enumerable) {
            writer.WriteStartObject();
            await WriteObject(context, writer, graphQLField, obj).ConfigureAwait(false);
            writer.WriteEndObject();
        }
    }

    private async Task ExecuteParallel(RequestContext context, object fieldValue, ResultJsonWriter writer, GraphQLField graphQLField, GraphQLParallel parallelOptions) {
        var results = new ConcurrentDictionary<int, JsonWriter>();

        var values = ((IEnumerable)fieldValue).OfType<object>();
        await values.AsyncParallelForEach(async data => {
            var subWriter = new JsonWriter(_scalarRegistry);
            results.TryAdd(data.Index, subWriter);

            subWriter.WriteStartObject();
            await WriteObject(context, subWriter, graphQLField, data.Value).ConfigureAwait(false);
            subWriter.WriteEndObject();
        }).ConfigureAwait(false);

        if(parallelOptions.SortResults) {
            foreach(var result in results.OrderBy(x => x.Key))
                await writer.WriteRaw(result.Value).ConfigureAwait(false);
        } else {
            foreach(var result in results)
                await writer.WriteRaw(result.Value).ConfigureAwait(false);
        }
    }

    private async Task WriteObject(RequestContext context, JsonWriter writer, GraphQLField graphQLField, object obj) {
        var resultContext = new ResultContext(obj, context, writer);
        var resultVisitor = new ResultVisitor(_fragmentAccessor, _valueAccessor, _dependencyResolver, _wrapperRegistry, _scalarRegistry);
        await resultVisitor.VisitAsync(graphQLField.SelectionSet, resultContext).ConfigureAwait(false);
    }

    private OperationDescriptor GetHandler(GraphQLField graphQLField) {
        return GetOperationsCollection().First(x => x.Name.Equals(graphQLField.Name.StringValue, StringComparison.InvariantCultureIgnoreCase));
    }

    private IEnumerable<OperationDescriptor> GetOperationsCollection() {
        return _operationType switch {
            OperationType.Query => _options.QueryOperations,
            OperationType.Mutation => _options.MutationsOperations,
            OperationType.Subscription => _options.SubscriptionOperations,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private async IAsyncEnumerable<object?> GetResult<T>(OperationDescriptor operationDescriptor, object query, ArgumentBuilder argumentBuilder, GraphQLField node) {
        var arguments = await BuildArguments<T>(operationDescriptor, argumentBuilder, node);
        if(_operationType is OperationType.Query or OperationType.Mutation) {
            var resultTask = await ExecuteMethod<T>(operationDescriptor, query, arguments).ConfigureAwait(false);
            yield return resultTask;
        } else {
            var asyncEnumerable = (IAsyncEnumerable<T>)operationDescriptor.Method.Invoke(query, arguments)!;
            await foreach(var obj in asyncEnumerable.WithCancellation(_context.CancellationToken).ConfigureAwait(false)) {
                yield return obj;
            }
        }
    }

    private static async Task<object?> ExecuteMethod<T>(OperationDescriptor operationDescriptor, object query, object?[] arguments) {
        return await ReflectionCache.ExecuteMethod(operationDescriptor.Method, query, arguments).ConfigureAwait(false);
    }

    private static async Task<object?[]> BuildArguments<T>(OperationDescriptor operationDescriptor, ArgumentBuilder argumentBuilder, GraphQLField node) {
        var isFederated = operationDescriptor.Method.HasCustomAttribute<GraphQLFederatedAttribute>();
        var arguments = await argumentBuilder.Build(node, !isFederated).ConfigureAwait(false);
        return arguments;
    }
}