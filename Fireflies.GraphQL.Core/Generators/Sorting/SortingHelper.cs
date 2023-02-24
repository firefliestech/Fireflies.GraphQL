using System.Reflection;
using Fireflies.GraphQL.Abstractions.Sorting;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Generators.Sorting;

public static class SortingHelper {
    public static async Task<IEnumerable<TElement>> WrapEnumerableTaskResult<TElement, TSort>(Task<IEnumerable<TElement>?> resultTask, TSort sortType, GraphQLField rootField, IGraphQLContext graphQLContext) {
        var taskResult = await resultTask;
        if(taskResult == null)
            return null;

        var sortNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "sort");
        if(sortNode == null)
            return taskResult;

        var sorter = new SortingBuilder<TElement, TSort>(taskResult, sortType);
        await sorter.VisitAsync(sortNode, graphQLContext);
        return sorter.Result.ToArray();
    }

    public static IEnumerable<TElement>? WrapEnumerableResult<TElement, TSort>(IEnumerable<TElement>? result, TSort sortType, GraphQLField graphQLField, IGraphQLContext graphQLContext) {
        return WrapEnumerableTaskResult(Task.FromResult(result), sortType, graphQLField, graphQLContext).Result;
    }

    private class SortingBuilder<TElement, TSort> : ASTVisitor<IGraphQLContext> {
        private readonly IEnumerable<TElement> _elements;
        private readonly Stack<PropertyInfo> _stack = new();

        public IEnumerable<TElement> Result { get; private set; }

        public SortingBuilder(IEnumerable<TElement> elements, TSort sort) {
            _elements = elements;
            Result = _elements;
        }

        protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IGraphQLContext context) {
            _stack.TryPeek(out var currentObject);
            var propertyInfo = (currentObject?.GetType() ?? typeof(TSort)).GetProperty(objectField.Name.StringValue, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)!;
            _stack.Push(propertyInfo);

            if(objectField.Value is GraphQLEnumValue enumValue) {
                var currentStack = _stack.ToArray();
                if(Result is IOrderedEnumerable<TElement> ordered) {
                    Result = enumValue.Name.StringValue == nameof(SortOrder.DESC) ? ordered.ThenByDescending(x => GetValue(x, currentStack)) : ordered.ThenBy(x => GetValue(x, currentStack));
                } else {
                    Result = enumValue.Name.StringValue == nameof(SortOrder.DESC) ? Result.OrderByDescending(x => GetValue(x, currentStack)) : Result.OrderBy(x => GetValue(x, currentStack));
                }
            } else {
                await VisitAsync(objectField.Value, context);
            }

            _stack.Pop();
        }

        private object? GetValue(object? element, PropertyInfo[] stack) {
            if(element == null)
                return null;

            var currentValue = element;
            for(var i = stack.Length - 1; i >= 0; i--) {
                var path = stack[i];
                var property = currentValue.GetType().GetProperty(path.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                currentValue = property?.GetValue(currentValue);
                if(currentValue == null)
                    return null;
            }

            return currentValue;
        }
    }
}