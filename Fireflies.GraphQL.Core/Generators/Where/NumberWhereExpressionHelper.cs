using System.Linq.Expressions;
using Fireflies.GraphQL.Abstractions.Where;

namespace Fireflies.GraphQL.Core.Generators.Where;

public static class NumberWhereExpressionHelper<T> where T : struct {
    public static Expression CreateExpression(string operation, Expression member, object? value) {
        if(operation.Equals(nameof(NumberWhere<T>.Equal), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.Equal);
        }

        if(operation.Equals(nameof(NumberWhere<T>.NotEqual), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.NotEqual);
        }

        if(operation.Equals(nameof(NumberWhere<T>.GreaterThanOrEq), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.GreaterThanOrEqual);
        }

        if(operation.Equals(nameof(NumberWhere<T>.GreaterThan), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.GreaterThan);
        }

        if(operation.Equals(nameof(NumberWhere<T>.LessThanOrEq), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.LessThanOrEqual);
        }

        if(operation.Equals(nameof(NumberWhere<T>.LessThan), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.LessThan);
        }

        throw new NotImplementedException($"Operation type '{operation}' is not implemented for {nameof(NumberWhere<T>)}");
    }
}