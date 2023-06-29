using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Where;

internal class QueryableWhereBuilder<TElement> {
    private readonly ValueAccessor _valueAccessor;
    public IQueryable<TElement> Result { get; private set; }

    public QueryableWhereBuilder(IQueryable<TElement> queryable, ValueAccessor valueAccessor) {
        _valueAccessor = valueAccessor;
        Result = queryable;
    }

    public ValueTask Build(ASTNode? node, IRequestContext context) {
        var whereExpressionBuilder = new WhereExpressionBuilder<TElement>(_valueAccessor);
        whereExpressionBuilder.VisitAsync(node, context).GetAwaiter().GetResult();
        if(whereExpressionBuilder.Result != null)
            Result = Result.Where(whereExpressionBuilder.Result);

        return ValueTask.CompletedTask;
    }
}