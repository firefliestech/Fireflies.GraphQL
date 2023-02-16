using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class ArgumentBuilder : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLArguments? _arguments;
    private readonly MethodInfo _methodInfo;
    private readonly VariableAccessor _variableAccessor;
    private readonly IGraphQLContext _context;
    private readonly Dictionary<string, ParameterInfo> _parameters;

    public Dictionary<string, object?> Values { get; set; } = new();

    private readonly Stack<object?> _stack = new();

    public ArgumentBuilder(GraphQLArguments? arguments, MethodInfo methodInfo, VariableAccessor variableAccessor, IGraphQLContext context) {
        _arguments = arguments;
        _methodInfo = methodInfo;
        _variableAccessor = variableAccessor;
        _context = context;
        _parameters = methodInfo.GetParameters().ToDictionary(x => x.Name!);
    }

    public async Task<object?[]> Build() {
        await VisitAsync(_arguments, _context).ConfigureAwait(false);
        return _methodInfo.GetParameters().Select(x => {
            if(x.ParameterType == typeof(CancellationToken))
                return _context.CancellationToken;

            if(Values.TryGetValue(x.Name!, out var result)) {
                return Convert.ChangeType(result, x.ParameterType);
            }

            return NullabilityChecker.IsNullable(x) ? null : x.DefaultValue;
        }).ToArray();
    }

    protected override async ValueTask VisitArgumentAsync(GraphQLArgument argument, IGraphQLContext context) {
        if(_parameters.TryGetValue(argument.Name.StringValue, out var parameterInfo)) {
            if(argument.Value.Kind == ASTNodeKind.ObjectValue) {
                var value = Activator.CreateInstance(parameterInfo.ParameterType);
                _stack.Push(value!);
                await VisitAsync(argument.Value, context);
                Values[parameterInfo.Name!] = _stack.Pop();
            } else {
                var before = _stack.Count;
                await VisitAsync(argument.Value, context);
                if(before == _stack.Count)
                    throw new InvalidOperationException($"NodeKind.{argument.Kind} is not supported");

                Values[parameterInfo.Name!] = _stack.Pop();
            }
        } else {
            throw new InvalidOperationException("Unmatched value");
        }
    }

    protected override ValueTask VisitNullValueAsync(GraphQLNullValue nullValue, IGraphQLContext context) {
        _stack.Push(null);
        return base.VisitNullValueAsync(nullValue, context);
    }

    protected override ValueTask VisitIntValueAsync(GraphQLIntValue intValue, IGraphQLContext context) {
        _stack.Push(int.Parse(intValue.Value));
        return ValueTask.CompletedTask;
    }

    protected override ValueTask VisitBooleanValueAsync(GraphQLBooleanValue booleanValue, IGraphQLContext context) {
        _stack.Push(booleanValue.BoolValue);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask VisitStringValueAsync(GraphQLStringValue stringValue, IGraphQLContext context) {
        _stack.Push(stringValue.Value.ToString());
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask VisitVariableAsync(GraphQLVariable variable, IGraphQLContext context) {
        var value = await _variableAccessor.GetValue(variable);
        _stack.Push(value);
    }

    protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, IGraphQLContext context) {
        var parent = _stack.Peek()!;
        var propertyField = parent.GetType().GetGraphQLProperty(objectField.Name.StringValue);
        if(Type.GetTypeCode(propertyField.PropertyType) == TypeCode.Object) {
            var value = Activator.CreateInstance(propertyField.PropertyType);
            _stack.Push(value!);
            await VisitAsync(objectField.Value, context);
            propertyField.SetValue(parent, _stack.Pop());
        } else {
            var value = await _variableAccessor.GetValue(objectField.Value);
            propertyField.SetValue(parent, value);
        }
    }
}