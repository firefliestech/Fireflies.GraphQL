using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Generators.Where;

public class EnumerableWhereBuilder<TElement> : ASTVisitor<RequestContext> {
    private readonly ValueAccessor _valueAccessor;
    public IEnumerable<TElement> Result { get; private set; }

    public EnumerableWhereBuilder(IEnumerable<TElement> queryable, ValueAccessor valueAccessor) {
        _valueAccessor = valueAccessor;
        Result = queryable;
    }


    protected override ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, RequestContext context) {
        var whereExpressionBuilder = new WhereExpressionBuilder<TElement>(objectField, _valueAccessor);
        whereExpressionBuilder.VisitAsync(objectField.Value, context).GetAwaiter().GetResult();
        if(whereExpressionBuilder.Result != null)
            Result = Result.Where(whereExpressionBuilder.Result.Compile());

        return ValueTask.CompletedTask;
    }
}