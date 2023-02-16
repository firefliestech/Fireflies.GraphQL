using GraphQLParser.AST;
using GraphQLParser.Visitors;
using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

internal class RequestValidator : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLRequest _request;
    private readonly FragmentAccessor _fragments;
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IGraphQLContext _context;
    private readonly List<string> _errors = new();
    private readonly HashSet<string> _usedVariables = new();
    private readonly Stack<Type> _fieldStack = new();
    private OperationType _operationType;

    public RequestValidator(GraphQLRequest request, FragmentAccessor fragments, GraphQLOptions options, IDependencyResolver dependencyResolver, IGraphQLContext context) {
        _request = request;
        _fragments = fragments;
        _options = options;
        _dependencyResolver = dependencyResolver;
        _context = context;
    }

    public async Task<List<string>> Validate(ASTNode startNode) {
        await VisitAsync(startNode, _context);

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
            var queryType = GetOperations().FirstOrDefault(x => x.Name == field.Name);
            if(queryType == null) {
                _errors.Add($"Cannot query field \"{field.Name}\" on type \"{_operationType}\"");
                return;
            }

            try {
                await AuthorizationHelper.Authorize(_dependencyResolver, queryType.Type);
                await AuthorizationHelper.Authorize(_dependencyResolver, queryType.Method);
            } catch(GraphQLUnauthorizedException) {
                _errors.Add($"Unauthorized access to query field \"{field.Name}\" on type \"Query\"");
            }

            var returnType = queryType.Method.DiscardTaskFromReturnType();
            returnType = returnType.GetGraphQLType();
            _fieldStack.Push(returnType);

            await ValidateFieldAgainstActualParameters(field, context, queryType.Method.GetParameters());

            pushed = true;
        } else {
            var currentType = _fieldStack.Peek();
            var currentTypeName = currentType.GetGraphQLType().GraphQLName();
            if(field.Name.StringValue == "__typename")
                return;

            var member = currentType.GetGraphQLMemberInfo(field.Name.StringValue);
            if(member == null || member.DeclaringType == typeof(object)) {
                _errors.Add($"Cannot query field \"{field.Name}\" on type \"{currentTypeName}\"");
                return;
            }

            try {
                await AuthorizationHelper.Authorize(_dependencyResolver, member);
            } catch(GraphQLUnauthorizedException) {
                _errors.Add($"Unauthorized access to query field \"{field.Name}\" on type \"{currentTypeName}\"");
            }

            switch(member) {
                case MethodInfo methodInfo:
                    _fieldStack.Push(methodInfo.ReturnType.GetGraphQLType());
                    pushed = true;
                    await ValidateFieldAgainstActualParameters(field, context, methodInfo.GetParameters());
                    break;
                case PropertyInfo propertyInfo:
                    _fieldStack.Push(propertyInfo.PropertyType.GetGraphQLType());
                    await ValidateFieldAgainstActualParameters(field, context, Array.Empty<ParameterInfo>());
                    pushed = true;
                    break;
                default:
                    _errors.Add($"Cannot query field \"{field.Name}\" on type \"{currentTypeName}\". Unknown member type");
                    break;
            }
        }

        if(!pushed)
            return;

        await ValidateSelectionSets(field, context);

        foreach(var directive in field.Directives ?? Enumerable.Empty<GraphQLDirective>())
            await VisitAsync(directive, context);

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
            var matching = currentType.GetAllClassesThatImplements().FirstOrDefault(x => x.GraphQLName() == inlineFragment.TypeCondition.Type.Name);
            if(matching != null) {
                _fieldStack.Push(matching);
                pushed = true;
            }
        }

        await base.VisitInlineFragmentAsync(inlineFragment, context);

        if(pushed)
            _fieldStack.Pop();
    }

    private async Task ValidateSelectionSets(GraphQLField field, IGraphQLContext context) {
        var selections = field.SelectionSet?.Selections ?? Enumerable.Empty<ASTNode>();
        var currentType = _fieldStack.Peek();
        var currentTypeName = currentType.GetGraphQLType().GraphQLName();
        if(!selections.Any() && currentType.IsClass && currentType != typeof(string)) {
            _errors.Add($"Field of type \"{field.Name}\" of type \"{currentTypeName}\" must have a selection of sub fields");
        }

        var any = false;
        foreach(var selection in selections) {
            any = true;
            await VisitAsync(selection, context);
        }

        if(!any) {

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
                await VisitAsync(arg, context);
            }
        }

        foreach(var unspecifiedParameter in remainingParameters.Where(unspecifiedParameter => !unspecifiedParameter.HasDefaultValue && !NullabilityChecker.IsNullable(unspecifiedParameter))) {
            _errors.Add($"Missing required argument \"{unspecifiedParameter.Name}\" on field \"{field.Name.StringValue}\".");
        }
    }

    protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, IGraphQLContext context) {
        await VisitAsync(await _fragments.GetFragment(fragmentSpread.FragmentName), context);
    }

    protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, IGraphQLContext context) {
        if(_fieldStack.Count == 0)
            return;

        foreach(var selection in fragmentDefinition.SelectionSet.Selections) {
            await VisitAsync(selection, context);
        }
    }
}