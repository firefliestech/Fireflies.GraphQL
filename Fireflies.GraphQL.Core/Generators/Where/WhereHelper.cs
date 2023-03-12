using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Where;

public static class WhereHelper {
    public static Task<IEnumerable<TElement>?> WhereEnumerableTaskResult<TElement>(Task<IEnumerable<TElement>?> resultTask, GraphQLField rootField, IGraphQLContext graphQLContext, ValueAccessor valueAccessor) {
        return resultTask.ContinueWith(taskResult => {
            if(taskResult.Result == null)
                return null;

            var whereNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "where");
            if(whereNode == null)
                return taskResult.Result;

            var asQueryable = taskResult.Result.AsQueryable();
            var whereBuilder = new EnumerableWhereBuilder<TElement>(asQueryable, valueAccessor);
            whereBuilder.VisitAsync(whereNode, graphQLContext).GetAwaiter().GetResult();
            return whereBuilder.Result;
        });
    }

    public static IEnumerable<TElement>? WhereEnumerableResult<TElement>(IEnumerable<TElement>? result, GraphQLField graphQLField, IGraphQLContext graphQLContext, ValueAccessor valueAccessor) {
        return WhereEnumerableTaskResult(Task.FromResult(result), graphQLField, graphQLContext, valueAccessor).Result;
    }

    public static Task<IQueryable<TElement>?> WhereQueryableTaskResult<TElement>(Task<IQueryable<TElement>?> resultTask, GraphQLField rootField, IGraphQLContext graphQLContext, ValueAccessor valueAccessor) {
        return resultTask.ContinueWith(taskResult => {
            if(taskResult.Result == null)
                return null;

            var whereNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "where");
            if(whereNode == null)
                return taskResult.Result;

            var whereBuilder = new QueryableWhereBuilder<TElement>(taskResult.Result, valueAccessor);
            whereBuilder.VisitAsync(whereNode, graphQLContext).GetAwaiter().GetResult();
            return whereBuilder.Result;
        });
    }

    public static IQueryable<TElement>? WhereQueryableResult<TElement>(IQueryable<TElement>? result, GraphQLField graphQLField, IGraphQLContext graphQLContext, ValueAccessor valueAccessor) {
        return WhereQueryableTaskResult(Task.FromResult(result), graphQLField, graphQLContext, valueAccessor).Result;
    }
}