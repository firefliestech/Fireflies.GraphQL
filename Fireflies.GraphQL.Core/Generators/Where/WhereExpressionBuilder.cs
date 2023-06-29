using System.Linq.Expressions;
using System.Reflection;
using Fireflies.GraphQL.Abstractions.Where;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Generators.Where;

internal class WhereExpressionBuilder : ASTVisitor<IRequestContext> {
    protected Stack<GraphQLObjectField> ParentFields = new();

    public Expression GetMemberPath<TElement>(ParameterExpression parameter) {
        var memberParts = QueryableExtensions.GetMemberParts<TElement>(string.Join(".", ParentFields.ToArray().Skip(1).Reverse().Select(x => x.Name)));
        var expression = memberParts.Aggregate((Expression)parameter, Expression.PropertyOrField);
        return expression;
    }
}

internal class WhereExpressionBuilder<TElement> : WhereExpressionBuilder {
    private readonly Stack<Type> _parentTypes = new();
    private readonly ValueAccessor _valueAccessor;
    private readonly ParameterExpression _parameter;
    private Expression? _result;

    public Expression<Func<TElement, bool>>? Result => _result != null ? (Expression<Func<TElement, bool>>)Expression.Lambda(_result, _parameter) : null;

    public WhereExpressionBuilder(ValueAccessor valueAccessor) {
        _valueAccessor = valueAccessor;
        _parameter = Expression.Parameter(typeof(TElement), "item");
        _parentTypes.Push(typeof(TElement));
    }

    public WhereExpressionBuilder(Stack<GraphQLObjectField> parentFields, ValueAccessor valueAccessor) : this(valueAccessor) {
        ParentFields = parentFields;
    }

    protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IRequestContext context) {
        ParentFields.Push(objectField);

        try {
            var parentType = _parentTypes.Peek();

            if(parentType.IsCollection(out var elementType)) {
                var collectionWhereBuilderType = typeof(CollectionWhereExpressionBuilder<,>).MakeGenericType(elementType, typeof(TElement));
                var collectionWhereBuilder = (CollectionWhereExpressionBuilder)Activator.CreateInstance(collectionWhereBuilderType, ParentFields, _valueAccessor, _parameter)!;
                await collectionWhereBuilder.VisitAsync(objectField, context);
                AppendResult(collectionWhereBuilder.Result);
                return;
            }

            if(objectField.Value is GraphQLObjectValue) {
                var property = parentType.GetProperty(objectField.Name.StringValue, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)!;
                _parentTypes.Push(property.PropertyType);
                await VisitAsync(objectField.Value, context).ConfigureAwait(false);
                _parentTypes.Pop();
            } else {
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
            }
        } finally {
            ParentFields.Pop();
        }
    }

    private async Task CreateOperationExpression(GraphQLObjectField objectField, bool negate, Func<Expression, ConstantExpression, Expression> factory) {
        var value = await _valueAccessor.GetValue(objectField.Value).ConfigureAwait(false);
        var member = GetMemberPath<TElement>(_parameter);
        var constantExpression = Expression.Constant(value);
        var resultExpression = factory(member, constantExpression);

        if(negate)
            resultExpression = Expression.Not(resultExpression);

        AppendResult(resultExpression);
    }

    private void AppendResult(Expression expression) {
        _result = _result != null ? Expression.AndAlso(_result, expression) : expression;
    }


}