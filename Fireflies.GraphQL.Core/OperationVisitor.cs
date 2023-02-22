using System.Collections;
using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.IoC.Abstractions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core;

internal class OperationVisitor : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly FragmentAccessor _fragments;
    private readonly ValueAccessor _valueAccessor;
    private readonly OperationType _operationType;
    private readonly IGraphQLContext _context;

    private static readonly MethodInfo GetResultMethod;

    static OperationVisitor() {
        GetResultMethod = typeof(OperationVisitor).GetMethod(nameof(GetResult), BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    public OperationVisitor(GraphQLOptions options, IDependencyResolver dependencyResolver, FragmentAccessor fragments, ValueAccessor valueAccessor, OperationType operationType, IGraphQLContext context) {
        _options = options;
        _dependencyResolver = dependencyResolver;
        _fragments = fragments;
        _valueAccessor = valueAccessor;
        _operationType = operationType;
        _context = context;
    }

    protected override async ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, IGraphQLContext context) {
        foreach(var selection in selectionSet.Selections) {
            switch(selection) {
                case GraphQLField graphQLField:
                    var operationDescriptor = GetHandler(graphQLField);

                    var operations = _dependencyResolver.Resolve(operationDescriptor.Type);

                    if(operations is IASTNodeHandler astNodeHandler) {
                        astNodeHandler.GraphQLNode = graphQLField;
                    }
                    var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, operationDescriptor.Method, _valueAccessor, _context, _dependencyResolver);

                    var returnType = operationDescriptor.Method.DiscardTaskFromReturnType();
                    var asyncEnumerable = (IAsyncEnumerable<object>)GetResultMethod.MakeGenericMethod(returnType).Invoke(this, new[] { operationDescriptor, operations, argumentBuilder, graphQLField })!;
                    await foreach(var result in asyncEnumerable.WithCancellation(context.CancellationToken)) {
                        var jObject = new JObject();

                        if(result is IEnumerable enumerable) {
                            var container = new JArray();
                            jObject.Add(graphQLField.Alias?.Name.StringValue ?? graphQLField.Name.StringValue, container);
                            foreach(var obj in enumerable) {
                                var subResult = new JObject();
                                container.Add(subResult);
                                foreach(var subSelection in graphQLField.SelectionSet!.Selections) {
                                    var resultVisitor = new ResultVisitor(obj, subResult, _fragments, _valueAccessor, _context, _dependencyResolver);
                                    await resultVisitor.VisitAsync(subSelection, context);
                                }
                            }
                        } else {
                            var subResult = new JObject();
                            jObject.Add(graphQLField.Alias?.Name.StringValue ?? graphQLField.Name.StringValue, subResult);
                            foreach(var subSelection in graphQLField.SelectionSet!.Selections) {
                                var resultVisitor = new ResultVisitor(result, subResult, _fragments, _valueAccessor, _context, _dependencyResolver);
                                await resultVisitor.VisitAsync(subSelection, context);
                            }
                        }

                        context.PublishResult(jObject);
                    }

                    break;
            }
        }
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
        var arguments = await argumentBuilder.Build(node);
        if(_operationType is OperationType.Query or OperationType.Mutation) {
            var resultTask = await operationDescriptor.Method.ExecuteMethod(query, arguments);
            yield return resultTask;
        } else {
            var asyncEnumerable = (IAsyncEnumerable<T>)operationDescriptor.Method.Invoke(query, arguments)!;
            await foreach(var obj in asyncEnumerable.WithCancellation(_context.CancellationToken)) {
                yield return obj;
            }
        }
    }
}