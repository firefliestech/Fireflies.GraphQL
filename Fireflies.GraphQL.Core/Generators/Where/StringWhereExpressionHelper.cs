using System.Linq.Expressions;
using Fireflies.GraphQL.Abstractions.Where;

namespace Fireflies.GraphQL.Core.Generators.Where;

public static class StringWhereExpressionHelper {
    public static Expression CreateExpression(string operation, Expression member, object? value) {
        if(operation.Equals(nameof(StringWhere.Equal), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.Equal);
        }

        if(operation.Equals(nameof(StringWhere.NotEqual), StringComparison.InvariantCultureIgnoreCase)) {
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, Expression.NotEqual);
        }

        if(operation.Equals(nameof(StringWhere.StartsWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression));
        }

        if(operation.Equals(nameof(StringWhere.DoesntStartWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, true, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression));
        }

        if(operation.Equals(nameof(StringWhere.EndsWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression));
        }

        if(operation.Equals(nameof(StringWhere.DoesntEndWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, true, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression));
        }

        if(operation.Equals(nameof(StringWhere.Contains), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, false, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression));
        }

        if(operation.Equals(nameof(StringWhere.DoesntContain), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
            return WhereExpressionBuilderHelper.CreateOperationExpression(member, value, true, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression));
        }

        throw new NotImplementedException($"Operation type '{operation}' is not implemented for {nameof(StringWhere)}");
    }
}