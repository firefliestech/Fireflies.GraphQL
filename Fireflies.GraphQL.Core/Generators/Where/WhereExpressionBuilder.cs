using System.Linq.Expressions;
using Fireflies.GraphQL.Abstractions.Where;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Generators.Where;

internal class WhereExpressionBuilder<TElement> : ASTVisitor<RequestContext> {
    private readonly GraphQLObjectField _parentField;
    private readonly ValueAccessor _valueAccessor;

    public Expression<Func<TElement, bool>>? Result { get; private set; }

    public WhereExpressionBuilder(GraphQLObjectField parentField, ValueAccessor valueAccessor) {
        _parentField = parentField;
        _valueAccessor = valueAccessor;
    }

    protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, RequestContext context) {
        // Where
        if(objectField.Name.StringValue.Equals(nameof(Where<int>.Equal), StringComparison.InvariantCultureIgnoreCase)) {
            await CreateOperationExpression(objectField, false, Expression.Equal).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(Where<int>.NotEqual), StringComparison.InvariantCultureIgnoreCase)) {
            await CreateOperationExpression(objectField, false, Expression.NotEqual).ConfigureAwait(false);
            return;
        }

        // NumberWhere
        if(objectField.Name.StringValue.Equals(nameof(NumberWhere<int>.GreaterThanOrEq), StringComparison.InvariantCultureIgnoreCase)) {
            await CreateOperationExpression(objectField, false, Expression.GreaterThanOrEqual).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(NumberWhere<int>.GreaterThan), StringComparison.InvariantCultureIgnoreCase)) {
            await CreateOperationExpression(objectField, false, Expression.GreaterThan).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(NumberWhere<int>.LessThanOrEq), StringComparison.InvariantCultureIgnoreCase)) {
            await CreateOperationExpression(objectField, false, Expression.LessThanOrEqual).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(NumberWhere<int>.LessThan), StringComparison.InvariantCultureIgnoreCase)) {
            await CreateOperationExpression(objectField, false, Expression.LessThan).ConfigureAwait(false);
            return;
        }

        // StringWhere
        if(objectField.Name.StringValue.Equals(nameof(StringWhere.StartsWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
            await CreateOperationExpression(objectField, false, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression)).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(StringWhere.DoesntStartWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
            await CreateOperationExpression(objectField, true, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression)).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(StringWhere.EndsWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;
            await CreateOperationExpression(objectField, false, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression)).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(StringWhere.DoesntEndWith), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;
            await CreateOperationExpression(objectField, true, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression)).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(StringWhere.Contains), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
            await CreateOperationExpression(objectField, false, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression)).ConfigureAwait(false);
            return;
        }

        if(objectField.Name.StringValue.Equals(nameof(StringWhere.DoesntContain), StringComparison.InvariantCultureIgnoreCase)) {
            var method = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
            await CreateOperationExpression(objectField, true, (memberExpression, valueExpression) => Expression.Call(memberExpression, method, valueExpression)).ConfigureAwait(false);
            return;
        }

        // Otherwise

        await VisitAsync(objectField.Value, context).ConfigureAwait(false);
    }

    private async Task CreateOperationExpression(GraphQLObjectField objectField, bool negate, Func<Expression, ConstantExpression, Expression> factory) {
        var parameter = Expression.Parameter(typeof(TElement), "item");
        var value = await _valueAccessor.GetValue(objectField).ConfigureAwait(false);
        var memberParts = QueryableExtensions.GetMemberParts<TElement>(_parentField.Name.StringValue);
        var member = memberParts.Aggregate((Expression)parameter, Expression.PropertyOrField);
        var constantExpression = Expression.Constant(value);
        var resultExpression = factory(member, constantExpression);

        if(negate)
            resultExpression = Expression.Not(resultExpression);

        Result = (Expression<Func<TElement, bool>>)Expression.Lambda(resultExpression, parameter);
    }
}