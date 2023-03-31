using GraphQLParser.AST;
using GraphQLParser.Visitors;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Exceptions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

internal class RequestValidator : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLRequest _request;
    private readonly FragmentAccessor _fragments;
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IGraphQLContext _context;
    private readonly WrapperRegistry _wrapperRegistry;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly ValueAccessor _valueAccessor;
    private readonly List<string> _errors = new();
    private readonly HashSet<string> _usedVariables = new();
    private readonly Stack<Type> _fieldStack = new();
    private OperationType _operationType;

    public RequestValidator(GraphQLRequest request, FragmentAccessor fragments, GraphQLOptions options, IDependencyResolver dependencyResolver, IGraphQLContext context, WrapperRegistry wrapperRegistry, ScalarRegistry scalarRegistry, ValueAccessor valueAccessor) {
        _request = request;
        _fragments = fragments;
        _options = options;
        _dependencyResolver = dependencyResolver;
        _context = context;
        _wrapperRegistry = wrapperRegistry;
        _scalarRegistry = scalarRegistry;
        _valueAccessor = valueAccessor;
    }

    public async Task<List<string>> Validate(ASTNode startNode) {
        await VisitAsync(startNode, _context).ConfigureAwait(false);

        ValidateVariables();

        return _errors;
    }

    private void ValidateVariables() {
        foreach(var unusedVariable in _request.Variables?.Keys.Except(_usedVariables) ?? Enumerable.Empty<string>()) {
            _errors.Add($"Variable ${unusedVariable} is never used");
        }
    }

    protected override ValueTask VisitVariableAsync(GraphQLVariable variable, IGraphQLContext context) {
        _usedVariables.Add(variable.Name.StringValue);

        if(!(_request.Variables?.ContainsKey(variable.Name.StringValue) ?? false))
            _errors.Add($"Variable ${variable.Name.StringValue} is not defined");

        return ValueTask.CompletedTask;
    }

    protected override ValueTask VisitOperationDefinitionAsync(GraphQLOperationDefinition operationDefinition, IGraphQLContext context) {
        _operationType = operationDefinition.Operation;

        if(operationDefinition.Operation == OperationType.Subscription && !context.IsWebSocket) {
            _errors.Add($"Operation type \"{_operationType}\" is only allowed if connected over websocket");
        }

        return base.VisitOperationDefinitionAsync(operationDefinition, context);
    }

    protected override async ValueTask VisitFieldAsync(GraphQLField field, IGraphQLContext context) {
        var pushed = false;

        if(_fieldStack.Count == 0) {
            var queryType = GetOperations().FirstOrDefault(x => x.Name.Equals(field.Name.StringValue, StringComparison.InvariantCultureIgnoreCase));
            if(queryType == null || queryType.Method.HasCustomAttribute<GraphQLInternalAttribute>()) {
                _errors.Add($"Cannot query field \"{field.Name}\" on type \"{_operationType}\"");
                return;
            }

            try {
                await AuthorizationHelper.Authorize(_dependencyResolver, queryType.Type).ConfigureAwait(false);
                await AuthorizationHelper.Authorize(_dependencyResolver, queryType.Method).ConfigureAwait(false);
            } catch(GraphQLUnauthorizedException) {
                _errors.Add($"Unauthorized access to query field \"{field.Name}\" on type \"Query\"");
            }

            var returnType = queryType.Method.DiscardTaskFromReturnType();
            returnType = returnType.GetGraphQLType();
            _fieldStack.Push(returnType);

            await ValidateFieldAgainstActualParameters(field, context, queryType.Method.GetParameters()).ConfigureAwait(false);

            pushed = true;
        } else {
            var currentType = _fieldStack.Peek();
            var currentTypeName = currentType.GetGraphQLType().GraphQLName();
            if(field.Name.StringValue == "__typename")
                return;

            var member = currentType.GetGraphQLMemberInfo(field.Name.StringValue);
            if(member == null || member.DeclaringType == typeof(object) || member.HasCustomAttribute<GraphQLInternalAttribute>()) {
                _errors.Add($"Cannot query field \"{field.Name}\" on type \"{currentTypeName}\"");
                return;
            }

            try {
                await AuthorizationHelper.Authorize(_dependencyResolver, member).ConfigureAwait(false);
            } catch(GraphQLUnauthorizedException) {
                _errors.Add($"Unauthorized access to query field \"{field.Name}\" on type \"{currentTypeName}\"");
            }

            switch(member) {
                case MethodInfo methodInfo:
                    _fieldStack.Push(methodInfo.ReturnType.GetGraphQLType());
                    pushed = true;
                    await ValidateFieldAgainstActualParameters(field, context, methodInfo.GetParameters()).ConfigureAwait(false);
                    break;
                case PropertyInfo propertyInfo:
                    _fieldStack.Push(propertyInfo.PropertyType.GetGraphQLType());
                    await ValidateFieldAgainstActualParameters(field, context, Array.Empty<ParameterInfo>()).ConfigureAwait(false);
                    pushed = true;
                    break;
                default:
                    _errors.Add($"Cannot query field \"{field.Name}\" on type \"{currentTypeName}\". Unknown member type");
                    break;
            }
        }

        if(!pushed)
            return;

        await ValidateSelectionSets(field, context).ConfigureAwait(false);

        foreach(var directive in field.Directives ?? Enumerable.Empty<GraphQLDirective>())
            await VisitAsync(directive, context).ConfigureAwait(false);

        if(pushed)
            _fieldStack.Pop();
    }

    private IEnumerable<OperationDescriptor> GetOperations() {
        switch(_operationType) {
            case OperationType.Query:
                return _options.QueryOperations;
            case OperationType.Mutation:
                return _options.MutationsOperations;
            case OperationType.Subscription:
                return _options.SubscriptionOperations;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override async ValueTask VisitInlineFragmentAsync(GraphQLInlineFragment inlineFragment, IGraphQLContext context) {
        var pushed = false;
        if(inlineFragment.TypeCondition != null) {
            var currentType = _fieldStack.Peek();
            var matching = currentType.GetAllClassesThatImplements().Select(x => _wrapperRegistry.GetWrapperOfSelf(x)).FirstOrDefault(x => x.Name == inlineFragment.TypeCondition.Type.Name.StringValue);
            if(matching != null) {
                _fieldStack.Push(matching);
                pushed = true;
            }
        }

        await base.VisitInlineFragmentAsync(inlineFragment, context).ConfigureAwait(false);

        if(pushed)
            _fieldStack.Pop();
    }

    private async Task ValidateSelectionSets(GraphQLField field, IGraphQLContext context) {
        var selections = field.SelectionSet?.Selections ?? Enumerable.Empty<ASTNode>();
        var currentType = _fieldStack.Peek();
        var currentTypeName = currentType.GetGraphQLType().GraphQLName();
        if(!selections.Any() && currentType.IsClass && currentType != typeof(string) && !currentType.IsSubclassOf(typeof(GraphQLId)) && !_scalarRegistry.Contains(currentType)) {
            _errors.Add($"Field of type \"{field.Name}\" of type \"{currentTypeName}\" must have a selection of sub fields");
        }

        foreach(var selection in selections) {
            await VisitAsync(selection, context).ConfigureAwait(false);
        }
    }

    private async Task ValidateFieldAgainstActualParameters(GraphQLField field, IGraphQLContext context, IEnumerable<ParameterInfo> parameters) {
        var remainingParameters = parameters.ToList();

        foreach(var arg in field.Arguments ?? Enumerable.Empty<GraphQLArgument>()) {
            var matchingParameter = parameters.FirstOrDefault(x => x.Name == arg.Name);
            if(matchingParameter == null) {
                _errors.Add($"Unknown argument \"{arg.Name}\" on field \"{field.Name.StringValue}\".");
            } else {
                remainingParameters.Remove(matchingParameter);
                var argumentValidator = new ArgumentValidator(matchingParameter, _valueAccessor, _errors);
                await argumentValidator.VisitAsync(arg.Value, context);
                await VisitAsync(arg, context).ConfigureAwait(false);
            }
        }

        var unspecifiedParameters = remainingParameters.Where(rp =>
            !rp.HasDefaultValue
            && !NullabilityChecker.IsNullable(rp)
            && !rp.HasCustomAttribute<ResolvedAttribute>()
            && !rp.HasCustomAttribute<EnumeratorCancellationAttribute>()
            && rp.ParameterType != typeof(ASTNode));

        foreach(var unspecifiedParameter in unspecifiedParameters) {
            _errors.Add($"Missing required argument \"{unspecifiedParameter.Name}\" on field \"{field.Name.StringValue}\".");
        }
    }

    protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, IGraphQLContext context) {
        await VisitAsync(await _fragments.GetFragment(fragmentSpread.FragmentName), context).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, IGraphQLContext context) {
        if(_fieldStack.Count == 0)
            return;

        foreach(var selection in fragmentDefinition.SelectionSet.Selections) {
            await VisitAsync(selection, context).ConfigureAwait(false);
        }
    }

    private class ArgumentValidator : ASTVisitor<IGraphQLContext> {
        private readonly ValueAccessor _valueAccessor;
        private readonly List<string> _errors;
        private readonly Stack<Type> _stack = new();

        public ArgumentValidator(ParameterInfo matchingParameter, ValueAccessor valueAccessor, List<string> errors) {
            _stack.Push(matchingParameter.ParameterType);
            _valueAccessor = valueAccessor;
            _errors = errors;
        }

        protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IGraphQLContext context) {
            var currentType = _stack.Peek();
            var member = currentType.GetProperty(objectField.Name.StringValue, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if(member == null) {
                _errors.Add($"Field with name \"{objectField.Name.StringValue}\" of type \"{currentType.GraphQLName()}\" does not exist");
            } else {
                var memberPropertyType = member.PropertyType.GetGraphQLBaseType();
                _stack.Push(memberPropertyType);
                switch(objectField.Value) {
                    case GraphQLFalseBooleanValue:
                    case GraphQLTrueBooleanValue:
                    case GraphQLBooleanValue:
                        if(memberPropertyType != typeof(bool))
                            AddTypeError(objectField, currentType, memberPropertyType);
                        break;
                    case GraphQLIntValue:
                        if(memberPropertyType != typeof(int))
                            AddTypeError(objectField, currentType, memberPropertyType);
                        break;
                    case GraphQLListValue:
                        // TODO: Handle?
                        break;

                    case GraphQLNullValue:
                        // TODO: Handle?
                        break;

                    case GraphQLObjectValue:
                        // TODO: Handle?
                        break;
                    case GraphQLStringValue:
                        if(memberPropertyType != typeof(string))
                            AddTypeError(objectField, currentType, memberPropertyType);
                        break;
                    case GraphQLVariable:
                        // TODO: Handle?
                        break;
                    case GraphQLEnumValue:
                        // TODO: Handle?
                        break;
                    case GraphQLFloatValue:
                        if(memberPropertyType != typeof(decimal))
                            AddTypeError(objectField, currentType, memberPropertyType);
                        break;
                }

                await VisitAsync(objectField.Value, context);
                _stack.Pop();
            }
        }

        protected override ValueTask VisitVariableAsync(GraphQLVariable variable, IGraphQLContext context) {
            var jsonElement = _valueAccessor.GetVariable(variable.Name.StringValue);
            if(jsonElement == null)
                return ValueTask.CompletedTask;

            var expectedType = _stack.Peek();
            var variableValue = _valueAccessor.GetVariable(variable.Name.StringValue);

            if(variableValue == null) {
                if(Nullable.GetUnderlyingType(expectedType) == null)
                    _errors.Add($"Value for variable with name \"{variable.Name.StringValue}\" must not be null");
                return ValueTask.CompletedTask;
            }

            var valueType = variableValue.GetType();
            if(IsNumber(expectedType)) {
                if(!IsNumber(valueType))
                    AddTypeError(variable, expectedType);
            } else if(expectedType == typeof(string)) {
                if(valueType != typeof(string))
                    AddTypeError(variable, expectedType);
            } else if(expectedType == typeof(bool)) {
                if(valueType != typeof(bool))
                    AddTypeError(variable, expectedType);
            }

            return ValueTask.CompletedTask;
        }

        private bool IsNumber(Type expectedType) {
            var typeCode = Type.GetTypeCode(expectedType);
            return typeCode switch {
                TypeCode.SByte => true,
                TypeCode.Byte => true,
                TypeCode.Int16 => true,
                TypeCode.UInt16 => true,
                TypeCode.Int32 => true,
                TypeCode.UInt32 => true,
                TypeCode.Int64 => true,
                TypeCode.UInt64 => true,
                TypeCode.Single => true,
                TypeCode.Double => true,
                TypeCode.Decimal => true,
                _ => false
            };
        }

        private void AddTypeError(GraphQLObjectField objectField, Type currentType, Type memberPropertyType) {
            _errors.Add($"Value for field with name \"{objectField.Name.StringValue}\" of type \"{currentType.Name}\" must be of type {memberPropertyType.GetPrimitiveGraphQLName()}");
        }

        private void AddTypeError(GraphQLVariable variable, Type memberPropertyType) {
            _errors.Add($"Value for variable with name \"{variable.Name.StringValue}\" must be of type {memberPropertyType.GetPrimitiveGraphQLName()}");
        }
    }
}