﻿using System.Collections;
using System.Reflection;
using Fireflies.GraphQL.Core.Json;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.IoC.Abstractions;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class OperationVisitor : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly FragmentAccessor _fragmentAccessor;
    private readonly ValueAccessor _valueAccessor;
    private readonly OperationType _operationType;
    private readonly IGraphQLContext _context;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly DataJsonWriter? _writer;
    private readonly WrapperRegistry _wrapperRegistry;

    private static readonly MethodInfo GetResultMethod;

    static OperationVisitor() {
        GetResultMethod = typeof(OperationVisitor).GetMethod(nameof(GetResult), BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    public OperationVisitor(GraphQLOptions options, IDependencyResolver dependencyResolver, FragmentAccessor fragmentAccessor, ValueAccessor valueAccessor, WrapperRegistry wrapperRegistry, OperationType operationType, IGraphQLContext context, ScalarRegistry scalarRegistry, DataJsonWriter? writer) {
        _options = options;
        _dependencyResolver = dependencyResolver;
        _fragmentAccessor = fragmentAccessor;
        _valueAccessor = valueAccessor;
        _wrapperRegistry = wrapperRegistry;

        _operationType = operationType;
        _context = context;
        _scalarRegistry = scalarRegistry;
        _writer = writer;
    }

    protected override async ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, IGraphQLContext context) {
        foreach(var graphQLField in selectionSet.Selections.OfType<GraphQLField>()) {
            var operationDescriptor = GetHandler(graphQLField);
            var operations = _dependencyResolver.Resolve(operationDescriptor.Type);

            var returnType = ReflectionCache.GetReturnType(operationDescriptor.Method);
            var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, operationDescriptor.Method, _valueAccessor, _context, _dependencyResolver, new ResultContext().Push(returnType));
            var methodInvoker = ReflectionCache.GetGenericMethodInvoker(GetResultMethod, new [] { returnType }, typeof(OperationDescriptor), typeof(object), typeof(ArgumentBuilder), typeof(GraphQLField));
            var asyncEnumerable = (IAsyncEnumerable<object?>)methodInvoker(this, operationDescriptor, operations, argumentBuilder, graphQLField);

            await foreach(var result in asyncEnumerable.WithCancellation(context.CancellationToken).ConfigureAwait(false)) {
                var writer = _writer ?? new DataJsonWriter(_scalarRegistry);

                var fieldName = graphQLField.Alias?.Name.StringValue ?? graphQLField.Name.StringValue;
                if(result is IEnumerable enumerable) {
                    writer.WriteStartArray(fieldName);
                    foreach(var obj in enumerable) {
                        writer.WriteStartObject();
                        await WriteObject(context, writer, graphQLField, obj);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                } else {
                    if(result != null) {
                        writer.WriteStartObject(fieldName);
                        await WriteObject(context, writer, graphQLField, result);
                        writer.WriteEndObject();
                    } else {
                        writer.WriteNull(fieldName);
                    }
                }

                context.PublishResult(writer);
            }
        }
    }

    private async Task WriteObject(IGraphQLContext context, DataJsonWriter writer, GraphQLField graphQLField, object obj) {
        var resultVisitor = new ResultVisitor(obj, writer, _fragmentAccessor, _valueAccessor, _context, _dependencyResolver, _wrapperRegistry);
        await resultVisitor.VisitAsync(graphQLField.SelectionSet, context).ConfigureAwait(false);
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
        var arguments = await argumentBuilder.Build(node).ConfigureAwait(false);
        if(_operationType is OperationType.Query or OperationType.Mutation) {
            var resultTask = await ReflectionCache.ExecuteMethod(operationDescriptor.Method, query, arguments).ConfigureAwait(false);
            yield return resultTask;
        } else {
            var asyncEnumerable = (IAsyncEnumerable<T>)operationDescriptor.Method.Invoke(query, arguments)!;
            await foreach(var obj in asyncEnumerable.WithCancellation(_context.CancellationToken).ConfigureAwait(false)) {
                yield return obj;
            }
        }
    }
}