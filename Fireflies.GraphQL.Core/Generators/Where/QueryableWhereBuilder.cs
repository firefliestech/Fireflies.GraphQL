using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Generators.Where;

internal class QueryableWhereBuilder<TElement> : ASTVisitor<IGraphQLContext> {
    private readonly ValueAccessor _valueAccessor;
    public IQueryable<TElement> Result { get; private set; }

    public QueryableWhereBuilder(IQueryable<TElement> queryable, ValueAccessor valueAccessor) {
        _valueAccessor = valueAccessor;
        Result = queryable;
    }

    protected override ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IGraphQLContext context) {
        var inner = new WhereExpressionBuilder<TElement>(objectField, _valueAccessor);
        inner.VisitAsync(objectField.Value, context).GetAwaiter().GetResult();
        if(inner.Result != null)
            Result = Result.Where(inner.Result);

        return ValueTask.CompletedTask;
    }
}