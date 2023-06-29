using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Where;

public class EnumerableWhereBuilder<TElement> {
    private readonly ValueAccessor _valueAccessor;
    public IEnumerable<TElement> Result { get; private set; }

    public EnumerableWhereBuilder(IEnumerable<TElement> queryable, ValueAccessor valueAccessor) {
        _valueAccessor = valueAccessor;
        Result = queryable;
    }

    public ValueTask Build(ASTNode? node, IRequestContext context) {
        var whereExpressionBuilder = new WhereExpressionBuilder<TElement>(_valueAccessor);
        whereExpressionBuilder.VisitAsync(node, context).GetAwaiter().GetResult();
        if(whereExpressionBuilder.Result != null)
            Result = Result.Where(whereExpressionBuilder.Result.Compile());

        return ValueTask.CompletedTask;
    }
}