using System.Linq.Expressions;
using System.Reflection;
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

        var parentType = _parentTypes.Peek();

        if(parentType.IsCollection(out var elementType)) {
            var collectionWhereBuilderType = typeof(CollectionWhereExpressionBuilder<,>).MakeGenericType(elementType, typeof(TElement));
            var collectionWhereBuilder = (CollectionWhereExpressionBuilder)Activator.CreateInstance(collectionWhereBuilderType, ParentFields, _valueAccessor, _parameter)!;
            await collectionWhereBuilder.VisitAsync(objectField, context);
            AppendResult(collectionWhereBuilder.Result);
        } else if(objectField.Value is GraphQLObjectValue) {
            var property = parentType.GetProperty(objectField.Name.StringValue, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)!;
            _parentTypes.Push(property.PropertyType);
            await VisitAsync(objectField.Value, context).ConfigureAwait(false);
            _parentTypes.Pop();
        } else {
            var operation = objectField.Name.StringValue;
            var memberPath = GetMemberPath<TElement>(_parameter);
            var value = await _valueAccessor.GetValue(_parentTypes.Peek(), objectField.Value).ConfigureAwait(false);

            if(parentType.IsAssignableTo(typeof(string))) {
                AppendResult(StringWhereExpressionHelper.CreateExpression(operation, memberPath, value));
            } else if(parentType.IsAssignableTo(typeof(int))) {
                AppendResult(NumberWhereExpressionHelper<int>.CreateExpression(operation, memberPath, value));
            } else if(parentType.IsAssignableTo(typeof(decimal))) {
                AppendResult(NumberWhereExpressionHelper<decimal>.CreateExpression(operation, memberPath, value));
            } else if(parentType.IsAssignableTo(typeof(bool))) {
                AppendResult(BooleanWhereExpressionHelper.CreateExpression(operation, memberPath, value));
            }
        }

        ParentFields.Pop();
    }

    private void AppendResult(Expression? expression) {
        if(expression == null)
            return;

        _result = _result != null ? Expression.AndAlso(_result, expression) : expression;
    }
}