using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Where;

public static class WhereHelper {
    public static async Task<IEnumerable<TElement>?> WhereEnumerableTaskResult<TElement>(Task<IEnumerable<TElement>?> resultTask, GraphQLField rootField, IRequestContext graphQLContext, ValueAccessor valueAccessor) {
        var taskResult = await resultTask.ConfigureAwait(false);

        if(taskResult == null)
            return null;

        var whereNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "where");
        if(whereNode == null)
            return taskResult;

        var asQueryable = taskResult.AsQueryable();
        var whereBuilder = new EnumerableWhereBuilder<TElement>(asQueryable, valueAccessor);
        whereBuilder.Build(whereNode, graphQLContext).GetAwaiter().GetResult();
        return whereBuilder.Result;
    }

    public static IEnumerable<TElement>? WhereEnumerableResult<TElement>(IEnumerable<TElement>? result, GraphQLField graphQLField, IRequestContext graphQLContext, ValueAccessor valueAccessor) {
        return WhereEnumerableTaskResult(Task.FromResult(result), graphQLField, graphQLContext, valueAccessor).Result;
    }

    public static async Task<IQueryable<TElement>?> WhereQueryableTaskResult<TElement>(Task<IQueryable<TElement>?> resultTask, GraphQLField rootField, IRequestContext graphQLContext, ValueAccessor valueAccessor) {
        var taskResult = await resultTask.ConfigureAwait(false);
        if(taskResult == null)
            return null;

        var whereNode = rootField.Arguments?.FirstOrDefault(x => x.Name == "where");
        if(whereNode == null)
            return taskResult;

        var whereBuilder = new QueryableWhereBuilder<TElement>(taskResult, valueAccessor);
        whereBuilder.Build(whereNode, graphQLContext).GetAwaiter().GetResult();
        return whereBuilder.Result;
    }

    public static IQueryable<TElement>? WhereQueryableResult<TElement>(IQueryable<TElement>? result, GraphQLField graphQLField, IRequestContext graphQLContext, ValueAccessor valueAccessor) {
        return WhereQueryableTaskResult(Task.FromResult(result), graphQLField, graphQLContext, valueAccessor).Result;
    }
}