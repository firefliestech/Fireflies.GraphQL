using System.Linq.Expressions;
using System.Reflection;
using Fireflies.GraphQL.Abstractions.Where;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Where;

internal class CollectionWhereExpressionBuilder : WhereExpressionBuilder {
    public MethodCallExpression Result { get; protected set; }
}

internal class CollectionWhereExpressionBuilder<TElement, TParentElementType> : CollectionWhereExpressionBuilder {
    private readonly ParameterExpression _parameter;
    private readonly ValueAccessor _valueAccessor;
    private readonly Stack<Type> _parentTypes = new();
    
    public CollectionWhereExpressionBuilder(Stack<GraphQLObjectField> parentFields, ValueAccessor valueAccessor, ParameterExpression parameter) {
        _parameter = parameter;
        _valueAccessor = valueAccessor;
        _parentTypes.Push(typeof(TElement));
        ParentFields = parentFields;
    }

    protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IRequestContext context) {
        if(objectField.Name.StringValue.Equals(nameof(CollectionWhere<TElement>.Any), StringComparison.InvariantCultureIgnoreCase)) {
            var subWhereExpressionBuilder = new WhereExpressionBuilder<TElement>(_valueAccessor);
            await subWhereExpressionBuilder.VisitAsync(objectField.Value, context);

            var method = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).First(x => x.Name == nameof(Enumerable.Any) && x.GetParameters().Length == 2).MakeGenericMethod(typeof(TElement));
            var expressionCall = Expression.Call(null, method, GetMemberPath<TParentElementType>(_parameter), subWhereExpressionBuilder.Result);

            Result = expressionCall;
        }
    }
}