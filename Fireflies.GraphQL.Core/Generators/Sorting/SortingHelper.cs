using System.Reflection;
using Fireflies.GraphQL.Abstractions.Sorting;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.Utility.Reflection;
using Fireflies.Utility.Reflection.Fasterflect;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Generators.Sorting;

public static class SortingHelper {
    public static async Task<IEnumerable<TElement>?> SortEnumerableTaskResult<TElement, TSort>(Task<IEnumerable<TElement>?> resultTask, TSort sortType, GraphQLField rootField, IRequestContext graphQLContext) {
        var taskResult = await resultTask.ConfigureAwait(false);
        if(taskResult == null)
            return null;

        var sortNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "sort");
        if(sortNode == null)
            return taskResult;

        var sorter = new EnumerableSortingBuilder<TElement>(taskResult);
        await sorter.VisitAsync(sortNode, graphQLContext).ConfigureAwait(false);
        return sorter.Result;
    }

    public static IEnumerable<TElement>? SortEnumerableResult<TElement, TSort>(IEnumerable<TElement>? result, TSort sortType, GraphQLField graphQLField, IRequestContext graphQLContext) {
        return SortEnumerableTaskResult(Task.FromResult(result), sortType, graphQLField, graphQLContext).Result;
    }

    public static async Task<IQueryable<TElement>?> SortQueryableTaskResult<TElement, TSort>(Task<IQueryable<TElement>?> resultTask, TSort sortType, GraphQLField rootField, IRequestContext graphQLContext) {
        var taskResult = await resultTask.ConfigureAwait(false);

        if(taskResult == null)
            return null;

        var sortNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "sort");
        if(sortNode == null)
            return taskResult;

        var sorter = new QueryableSortingBuilder<TElement>(taskResult);
        await sorter.VisitAsync(sortNode, graphQLContext).ConfigureAwait(false);
        return sorter.Result;
    }

    public static IQueryable<TElement>? SortQueryableResult<TElement, TSort>(IQueryable<TElement>? result, TSort sortType, GraphQLField graphQLField, IRequestContext graphQLContext) {
        return SortQueryableTaskResult(Task.FromResult(result), sortType, graphQLField, graphQLContext).Result;
    }

    private class EnumerableSortingBuilder<TElement> : ASTVisitor<IRequestContext> {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly MethodInfo WaitForMethod;
        private bool _firstSort;

        static EnumerableSortingBuilder() {
            WaitForMethod = typeof(EnumerableSortingBuilder<TElement>).GetMethod(nameof(WaitFor), BindingFlags.NonPublic | BindingFlags.Instance)!;
        }

        public IEnumerable<TElement> Result { get; private set; }

        public EnumerableSortingBuilder(IEnumerable<TElement> elements) {
            Result = elements;
            _firstSort = true;
        }

        protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IRequestContext context) {
            if(objectField.Value is GraphQLEnumValue enumValue) {
                var desc = enumValue.Name.StringValue == nameof(SortOrder.DESC);
                if(!_firstSort) {
                    var orderedQueryable = (IOrderedEnumerable<TElement>)Result;
                    Result = desc ? orderedQueryable.ThenByDescending(x => GetValue(x, objectField.Name.StringValue)) : orderedQueryable.OrderBy(x => GetValue(x, objectField.Name.StringValue));
                } else {
                    Result = desc ? Result.OrderByDescending(x => GetValue(x, objectField.Name.StringValue)) : Result.OrderBy(x => GetValue(x, objectField.Name.StringValue));
                    _firstSort = false;
                }
            } else {
                await VisitAsync(objectField.Value, context).ConfigureAwait(false);
            }
        }

        private object? GetValue(object? element, string field) {
            if(element == null)
                return null;

            var property = element.GetType().GetMember(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase).First();
            var currentValueMethod = property switch {
                PropertyInfo propertyInfo => propertyInfo.GetMethod!,
                MethodInfo methodInfo => methodInfo,
                _ => throw new ArgumentOutOfRangeException()
            };

            if(!currentValueMethod.ReturnType.IsTask(out var elementType)) {
                return Reflect.Method(currentValueMethod, Array.Empty<Type>())(element);
            }

            var waitMethodInvoker = Reflect.Method(WaitForMethod, new[] { elementType! }, typeof(MethodBase), typeof(object));
            var valueTask = (Task<object?>)waitMethodInvoker.Invoke(this, currentValueMethod, element)!;
            return valueTask.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private async Task<object?> WaitFor<T>(MethodBase valueMethod, object element) {
            var valueTask = (Task<T>)valueMethod.Invoke(element, null)!;
            return await valueTask.ConfigureAwait(false);
        }
    }

    private class QueryableSortingBuilder<TElement> : ASTVisitor<IRequestContext> {
        // ReSharper disable once StaticMemberInGenericType
        private bool _firstSort;

        public IQueryable<TElement> Result { get; private set; }

        public QueryableSortingBuilder(IQueryable<TElement> elements) {
            Result = elements;
            _firstSort = true;
        }

        protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IRequestContext context) {
            if(objectField.Value is GraphQLEnumValue enumValue) {
                var desc = enumValue.Name.StringValue == nameof(SortOrder.DESC);
                if(!_firstSort) {
                    var orderedQueryable = (IOrderedQueryable<TElement>)Result;
                    Result = desc ? orderedQueryable.ThenByMemberDescending(objectField.Name.StringValue) : orderedQueryable.ThenByMember(objectField.Name.StringValue);
                } else {
                    Result = desc ? Result.OrderByMemberDescending(objectField.Name.StringValue) : Result.OrderByMember(objectField.Name.StringValue);
                    _firstSort = false;
                }
            } else {
                await VisitAsync(objectField.Value, context).ConfigureAwait(false);
            }
        }
    }
}