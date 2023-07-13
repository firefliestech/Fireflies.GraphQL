using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Federation;
using Fireflies.GraphQL.Core.Json;
using Fireflies.Logging.Abstractions;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core;

public class OperationVisitor : ASTVisitor<OperationContext> {
    private readonly GraphQLOptions _options;
    private readonly ResultVisitor _resultVisitor;
    private readonly JsonWriterFactory _writerFactory;

    private static readonly MethodInfo GetResultMethod;
    private readonly IFirefliesLogger _logger;

    static OperationVisitor() {
        GetResultMethod = typeof(OperationVisitor).GetMethod(nameof(GetResult), BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    public OperationVisitor(GraphQLOptions options, IFirefliesLoggerFactory loggerFactory, ResultVisitor resultVisitor, JsonWriterFactory writerFactory) {
        _options = options;
        _resultVisitor = resultVisitor;
        _writerFactory = writerFactory;
        _logger = loggerFactory.GetLogger<OperationVisitor>();
    }

    protected override async ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, OperationContext context) {
        foreach(var graphQLField in selectionSet.Selections.OfType<GraphQLField>()) {
            var operationDescriptor = GetHandler(graphQLField, context);
            var operations = context.DependencyResolver.Resolve(operationDescriptor.Type);

            var returnType = ReflectionCache.GetReturnType(operationDescriptor.Method);
            var executeInParallel = operationDescriptor.Method.HasCustomAttribute<GraphQLParallel>(out var parallelAttribute);

            var resultContext = new ResultContext(returnType, context);
            resultContext.Path.Push(graphQLField.Name.StringValue);
            
            var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, operationDescriptor.Method, context, resultContext);
            var methodInvoker = ReflectionCache.GetGenericMethodInvoker(GetResultMethod, new[] { returnType }, typeof(OperationDescriptor), typeof(object), typeof(ArgumentBuilder), typeof(GraphQLField), typeof(OperationContext));
            try {
                var asyncEnumerable = (IAsyncEnumerable<object?>)methodInvoker(this, operationDescriptor, operations, argumentBuilder, graphQLField, context);
                await foreach(var result in asyncEnumerable.WithCancellation(context.CancellationToken).ConfigureAwait(false)) {
                    var writer = context.Writer ?? _writerFactory.CreateResultWriter();

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
                                await WriteObject(graphQLField, result, writer, context).ConfigureAwait(false);
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
                var writer = context.Writer ?? _writerFactory.CreateResultWriter();
                foreach(var error in fex.Node) {
                    var message = error["message"]!.GetValue<string>();
                    var code = error["extensions"]!["code"].GetValue<string>();

                    if(code != "GRAPHQL_VALIDATION_FAILED")
                        _logger.Error($"Error occurred during federated request. Message: {message}. Code: {code}");

                    writer.AddError(error);
                }

                await context.PublishResult(writer).ConfigureAwait(false);
            } catch(Exception ex) {
                _logger.Error(ex, "Exception occured while processing request");
                var writer = context.Writer ?? _writerFactory.CreateResultWriter();
                writer.AddError("GRAPHQL_EXECUTION_FAILED", "Internal server error occurred");
                await context.PublishResult(writer).ConfigureAwait(false);
            }

            resultContext.Path.Pop();
        }
    }

    private async IAsyncEnumerable<object?> GetResult<T>(OperationDescriptor operationDescriptor, object query, ArgumentBuilder argumentBuilder, GraphQLField node, OperationContext context) {
        var arguments = await BuildArguments(operationDescriptor, argumentBuilder, node);
        if(context.OperationType is OperationType.Query or OperationType.Mutation) {
            var resultTask = await ReflectionCache.ExecuteMethod(operationDescriptor.Method, query, arguments).ConfigureAwait(false);
            yield return resultTask;
        } else {
            var asyncEnumerable = (IAsyncEnumerable<T>)operationDescriptor.Method.Invoke(query, arguments)!;
            await foreach(var obj in asyncEnumerable.WithCancellation(context.CancellationToken).ConfigureAwait(false)) {
                yield return obj;
            }
        }
    }

    private async Task ExecuteSynchronously(OperationContext context, IEnumerable enumerable, ResultJsonWriter writer, GraphQLField graphQLField) {
        foreach(var obj in enumerable) {
            writer.WriteStartObject();
            await WriteObject(graphQLField, obj, writer, context).ConfigureAwait(false);
            writer.WriteEndObject();
        }
    }

    private async Task ExecuteParallel(OperationContext context, object fieldValue, JsonWriter writer, GraphQLField graphQLField, GraphQLParallel parallelOptions) {
        var results = new ConcurrentDictionary<int, JsonWriter>();

        var values = ((IEnumerable)fieldValue).OfType<object>();
        await values.AsyncParallelForEach(async data => {
            var subWriter = writer.CreateSubWriter();
            results.TryAdd(data.Index, subWriter);

            subWriter.WriteStartObject();
            await WriteObject(graphQLField, data.Value, subWriter, context).ConfigureAwait(false);
            subWriter.WriteEndObject();
        }, parallelOptions.MaxDegreeOfParallelism).ConfigureAwait(false);

        if(parallelOptions.SortResults) {
            foreach(var result in results.OrderBy(x => x.Key))
                await writer.WriteRaw(result.Value).ConfigureAwait(false);
        } else {
            foreach(var result in results)
                await writer.WriteRaw(result.Value).ConfigureAwait(false);
        }
    }

    private async Task WriteObject(GraphQLField field, object data, JsonWriter writer, OperationContext context) {
        var resultContext = new ResultContext(data, context, writer);
        resultContext.Path.Push(field.Name.StringValue);
        await _resultVisitor.VisitAsync(field.SelectionSet, resultContext).ConfigureAwait(false);
        resultContext.Path.Pop();
    }

    private OperationDescriptor GetHandler(GraphQLField graphQLField, OperationContext context) {
        return GetOperationsCollection(context).First(x => x.Name.Equals(graphQLField.Name.StringValue, StringComparison.InvariantCultureIgnoreCase));
    }

    private IEnumerable<OperationDescriptor> GetOperationsCollection(OperationContext context) {
        return context.OperationType switch {
            OperationType.Query => _options.QueryOperations,
            OperationType.Mutation => _options.MutationsOperations,
            OperationType.Subscription => _options.SubscriptionOperations,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static async Task<object?[]> BuildArguments(OperationDescriptor operationDescriptor, ArgumentBuilder argumentBuilder, GraphQLField node) {
        var isFederated = operationDescriptor.Method.HasCustomAttribute<GraphQLFederatedAttribute>();
        var arguments = await argumentBuilder.Build(node, !isFederated).ConfigureAwait(false);
        return arguments;
    }
}