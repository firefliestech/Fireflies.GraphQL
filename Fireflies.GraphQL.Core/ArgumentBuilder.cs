using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Json;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class ArgumentBuilder : ASTVisitor<IRequestContext> {
    private readonly GraphQLArguments? _arguments;
    private readonly MethodInfo _methodInfo;
    private readonly IRequestContext _requestContext;
    private readonly ResultContext _resultContext;
    private readonly Dictionary<string, ParameterInfo> _parameters;

    public Dictionary<string, object?> Values { get; set; } = new();

    private readonly Stack<object?> _stack = new();

    public ArgumentBuilder(GraphQLArguments? arguments, MethodInfo methodInfo, IRequestContext context, ResultContext resultContext) {
        _arguments = arguments;
        _methodInfo = methodInfo;
        _requestContext = context;
        _resultContext = resultContext;
        _parameters = methodInfo.GetParameters().ToDictionary(x => x.Name!);
    }

    public async Task<object?[]> Build<TASTNode>(TASTNode node, bool addNodeArguments = true) where TASTNode : ASTNode {
        if(addNodeArguments)
            await VisitAsync(_arguments, _requestContext).ConfigureAwait(false);

        return ReflectionCache.GetParameters(_methodInfo).Select(x => {
            if(x.HasCustomAttribute<EnumeratorCancellationAttribute>() && x.HasDefaultValue)
                return null;

            if(typeof(TASTNode).IsAssignableTo(x.ParameterType))
                return node;

            if(typeof(GraphQLDocument).IsAssignableTo(x.ParameterType))
                return _requestContext.Document;

            if(x.ParameterType == typeof(CancellationToken))
                return !x.HasCustomAttribute<EnumeratorCancellationAttribute>() ? _requestContext.CancellationToken : default;

            if(x.ParameterType.IsAssignableTo(typeof(IErrorCollection)))
                return _resultContext.Writer;

            if(x.ParameterType == typeof(ResultContext))
                return _resultContext;

            if(x.ParameterType == typeof(IRequestContext) || (x.ParameterType == typeof(OperationContext) && _requestContext is OperationContext))
                return _requestContext;

            if(x.ParameterType == typeof(ValueAccessor))
                return _requestContext.ValueAccessor;

            if(x.ParameterType == typeof(FragmentAccessor))
                return _requestContext.FragmentAccessor;

            if(x.ParameterType.IsAssignableTo(typeof(IGraphQLPath)))
                return new GraphQLPath(_resultContext.Path);

            if(x.HasCustomAttribute<ResolvedAttribute>(out _))
                return _requestContext.DependencyResolver.Resolve(x.ParameterType);

            if(Values.TryGetValue(x.Name!, out var result)) {
                if(result != null && result.GetType().IsAssignableTo(x.ParameterType))
                    return result;

                if(result is JsonElement jsonElement) {
                    return jsonElement.Deserialize(x.ParameterType, DefaultJsonSerializerSettings.DefaultSettings);
                }

                return Convert.ChangeType(result, x.ParameterType);
            }

            if(x.HasDefaultValue)
                return x.DefaultValue;

            if(x.ParameterType.IsClass)
                return null;

            return NullabilityChecker.IsNullable(x) ? null : Activator.CreateInstance(x.ParameterType);
        }).ToArray();
    }

    protected override async ValueTask VisitArgumentAsync(GraphQLArgument argument, IRequestContext context) {
        if(!_parameters.TryGetValue(argument.Name.StringValue, out var parameterInfo))
            return;

        if(argument.Value.Kind == ASTNodeKind.ObjectValue) {
            var value = Activator.CreateInstance(parameterInfo.ParameterType)!;
            _stack.Push(value);
            await VisitAsync(argument.Value, context).ConfigureAwait(false);
            Values[parameterInfo.Name!] = _stack.Pop();
        } else {
            Values[parameterInfo.Name!] = await context.ValueAccessor!.GetValue(argument.Value).ConfigureAwait(false);
        }
    }

    protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IRequestContext context) {
        var parent = _stack.Peek()!;
        var propertyField = parent.GetType().GetGraphQLProperty(objectField.Name.StringValue);
        var underlyingType = Nullable.GetUnderlyingType(propertyField.PropertyType) ?? propertyField.PropertyType;

        if(Type.GetTypeCode(underlyingType) == TypeCode.Object) {
            var value = Activator.CreateInstance(underlyingType)!;
            _stack.Push(value);
            await VisitAsync(objectField.Value, context).ConfigureAwait(false);
            propertyField.SetValue(parent, _stack.Pop());
        } else {
            var value = await context.ValueAccessor!.GetValue(underlyingType, objectField.Value).ConfigureAwait(false);
            propertyField.SetValue(parent, value);
        }
    }
}