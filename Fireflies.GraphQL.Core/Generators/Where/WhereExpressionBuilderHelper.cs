using System.Linq.Expressions;

namespace Fireflies.GraphQL.Core.Generators.Where;

public static class WhereExpressionBuilderHelper {
    public static Expression CreateOperationExpression(Expression member, object? value, bool negate, Func<Expression, ConstantExpression, Expression> factory) {
        var resultExpression = factory(member, Expression.Constant(value));

        if(negate)
            resultExpression = Expression.Not(resultExpression);

        return resultExpression;
    }
}