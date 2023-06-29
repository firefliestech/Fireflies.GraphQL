using System.Linq.Expressions;
using Fireflies.GraphQL.Abstractions.Where;

namespace Fireflies.GraphQL.Core.Generators.Where;

public static class BooleanWhereExpressionHelper {
    public static Expression CreateExpression(string operation, Expression member, object? value) {
        if(operation.Equals(nameof(BooleanWhere.Equal), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.Equal);
        }

        if(operation.Equals(nameof(BooleanWhere.NotEqual), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.NotEqual);
        }

        throw new NotImplementedException($"Operation type '{operation}' is not implemented for {nameof(BooleanWhere)}");
    }
}