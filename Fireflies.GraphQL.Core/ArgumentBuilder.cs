using System.Reflection;
using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.IoC.Abstractions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class ArgumentBuilder : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLArguments? _arguments;
    private readonly MethodInfo _methodInfo;
    private readonly ValueAccessor _valueAccessor;
    private readonly IGraphQLContext _context;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly ResultContext _resultContext;
    private readonly Dictionary<string, ParameterInfo> _parameters;

    public Dictionary<string, object?> Values { get; set; } = new();

    private readonly Stack<object?> _stack = new();

    public ArgumentBuilder(GraphQLArguments? arguments, MethodInfo methodInfo, ValueAccessor valueAccessor, IGraphQLContext context, IDependencyResolver dependencyResolver, ResultContext resultContext) {
        _arguments = arguments;
        _methodInfo = methodInfo;
        _valueAccessor = valueAccessor;
        _context = context;
        _dependencyResolver = dependencyResolver;
        _resultContext = resultContext;
        _parameters = methodInfo.GetParameters().ToDictionary(x => x.Name!);
    }

    public async Task<object?[]> Build<TASTNode>(TASTNode node) where TASTNode : ASTNode {
        await VisitAsync(_arguments, _context).ConfigureAwait(false);
        return _methodInfo.GetParameters().Select(x => {
            if(x.HasCustomAttribute<EnumeratorCancellationAttribute>() && x.HasDefaultValue)
                return null;

            if(typeof(TASTNode).IsAssignableTo(x.ParameterType))
                return node;

            if(x.ParameterType == typeof(CancellationToken))
                return !x.HasCustomAttribute<EnumeratorCancellationAttribute>() ? _context.CancellationToken : default;

            if(x.ParameterType == typeof(ResultContext))
                return _resultContext;

            if (x.HasCustomAttribute<ResolvedAttribute>(out _))
                return _dependencyResolver.Resolve(x.ParameterType);

            if(Values.TryGetValue(x.Name!, out var result))
                return Convert.ChangeType(result, x.ParameterType);

            return NullabilityChecker.IsNullable(x) ? null : x.DefaultValue;
        }).ToArray();
    }

    protected override async ValueTask VisitArgumentAsync(GraphQLArgument argument, IGraphQLContext context) {
        if(_parameters.TryGetValue(argument.Name.StringValue, out var parameterInfo)) {
            if(argument.Value.Kind == ASTNodeKind.ObjectValue) {
                var value = Activator.CreateInstance(parameterInfo.ParameterType)!;
                _stack.Push(value);
                await VisitAsync(argument.Value, context).ConfigureAwait(false);
                Values[parameterInfo.Name!] = _stack.Pop();
            } else {
                Values[parameterInfo.Name!] = await _valueAccessor.GetValue(argument.Value).ConfigureAwait(false);
            }
        } else {
            throw new InvalidOperationException("Unmatched value");
        }
    }
    
    protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IGraphQLContext context) {
        var parent = _stack.Peek()!;
        var propertyField = parent.GetType().GetGraphQLProperty(objectField.Name.StringValue);
        var underlyingType = Nullable.GetUnderlyingType(propertyField.PropertyType) ?? propertyField.PropertyType;

        if(Type.GetTypeCode(underlyingType) == TypeCode.Object) {
            var value = Activator.CreateInstance(underlyingType)!;
            _stack.Push(value);
            await VisitAsync(objectField.Value, context).ConfigureAwait(false);
            propertyField.SetValue(parent, _stack.Pop());
        } else {
            var value = await _valueAccessor.GetValue(underlyingType, objectField.Value).ConfigureAwait(false);
            propertyField.SetValue(parent, value);
        }
    }
}