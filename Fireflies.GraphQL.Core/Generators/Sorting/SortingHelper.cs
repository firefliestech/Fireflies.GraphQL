using System.Reflection;
using Fireflies.GraphQL.Abstractions.Sorting;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Generators.Sorting;

public static class SortingHelper {
    public static Task<IQueryable<TElement>?> WrapEnumerableTaskResult<TElement, TSort>(Task<IQueryable<TElement>?> resultTask, TSort sortType, GraphQLField rootField, IGraphQLContext graphQLContext) {
        return resultTask.ContinueWith(taskResult => {
            if(taskResult.Result == null)
                return null;

            var sortNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "sort");
            if(sortNode == null)
                return taskResult.Result;

            var sorter = new SortingBuilder<TElement>(taskResult.Result);
            sorter.VisitAsync(sortNode, graphQLContext).GetAwaiter().GetResult();
            return sorter.Result;
        });
    }

    public static IQueryable<TElement>? WrapEnumerableResult<TElement, TSort>(IQueryable<TElement>? result, TSort sortType, GraphQLField graphQLField, IGraphQLContext graphQLContext) {
        return WrapEnumerableTaskResult(Task.FromResult(result), sortType, graphQLField, graphQLContext).Result;
    }

    private class SortingBuilder<TElement> : ASTVisitor<IGraphQLContext> {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly MethodInfo WaitForMethod;

        static SortingBuilder() {
            WaitForMethod = typeof(SortingBuilder<TElement>).GetMethod(nameof(WaitFor), BindingFlags.NonPublic | BindingFlags.Instance)!;
        }

        public IQueryable<TElement> Result { get; private set; }

        public SortingBuilder(IQueryable<TElement> elements) {
            Result = elements;
        }

        protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IGraphQLContext context) {
            if(objectField.Value is GraphQLEnumValue enumValue) {
                var desc = enumValue.Name.StringValue == nameof(SortOrder.DESC);
                Result = desc ? Result.OrderByDescending(x => GetValue(x, objectField.Name.StringValue)) : Result.OrderBy(x => GetValue(x, objectField.Name.StringValue));
            } else {
                await VisitAsync(objectField.Value, context);
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
                return currentValueMethod.Invoke(element, null);
            }

            var valueTask = (Task<object?>)WaitForMethod.MakeGenericMethod(elementType!).Invoke(this, new[] { currentValueMethod, element })!;
            return valueTask.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private async Task<object?> WaitFor<T>(MethodBase valueMethod, object element) {
            var valueTask = (Task<T>)valueMethod.Invoke(element, null)!;
            return await valueTask.ConfigureAwait(false);
        }
    }
}