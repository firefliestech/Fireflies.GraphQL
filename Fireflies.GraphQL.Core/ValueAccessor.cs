using System.Text.Json;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public class ValueAccessor {
    private readonly RequestContext _context;
    private readonly ValueVisitor _visitor;

    internal ValueAccessor(Dictionary<string, object?>? variables, RequestContext context) {
        _context = context;

        _visitor = new ValueVisitor(variables);
    }

    public Dictionary<string, object?> Variables => _visitor.Variables;

    public async Task<T?> GetValue<T>(ASTNode node) {
        var unconvertedValue = await GetValue(node).ConfigureAwait(false);
        if(unconvertedValue == null)
            return default;

        return (T)Convert.ChangeType(unconvertedValue, typeof(T));
    }

    public async Task<object?> GetValue(Type returnType, ASTNode node) {
        var visitorContext = new ValueVisitorContext(_context, returnType);
        await _visitor.VisitAsync(node, visitorContext).ConfigureAwait(false);
        return visitorContext.Result;
    }

    public async Task<object?> GetValue(ASTNode node) {
        var visitorContext = new ValueVisitorContext(_context, typeof(object));
        await _visitor.VisitAsync(node, visitorContext).ConfigureAwait(false);
        return visitorContext.Result;
    }

    public object? GetVariable(string variableName) {
        return _visitor.GetVariable(variableName);
    }

    private class ValueVisitor : ASTVisitor<ValueVisitorContext> {
        private readonly Dictionary<string, object?> _variables;

        public Dictionary<string, object?> Variables => _variables;

        public ValueVisitor(Dictionary<string, object?>? variables) {
            _variables = variables?.Select(x => new { x.Key, Value = x.Value == null ? null : ConvertToValue((JsonElement)x.Value) }).ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, object?>();
        }

        private object? ConvertToValue(JsonElement value) {
            return value.ValueKind switch {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Undefined => null,
                JsonValueKind.Null => null,
                JsonValueKind.Object => value,
                JsonValueKind.Array => value,
                _ => null
            };
        }

        protected override ValueTask VisitBooleanValueAsync(GraphQLBooleanValue booleanValue, ValueVisitorContext context) {
            context.Result = booleanValue.BoolValue;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitStringValueAsync(GraphQLStringValue stringValue, ValueVisitorContext context) {
            context.Result = stringValue.Value.ToString();
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitNullValueAsync(GraphQLNullValue nullValue, ValueVisitorContext context) {
            context.Result = null;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitIntValueAsync(GraphQLIntValue intValue, ValueVisitorContext context) {
            context.Result = int.Parse(intValue.Value);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitEnumValueAsync(GraphQLEnumValue enumValue, ValueVisitorContext context) {
            context.Result = Enum.Parse(context.ReturnType, enumValue.Name.StringValue);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitVariableAsync(GraphQLVariable variable, ValueVisitorContext context) {
            context.Result = GetVariable(variable.Name.StringValue);
            return ValueTask.CompletedTask;
        }

        public object? GetVariable(string variableName) {
            return _variables.TryGetValue(variableName, out var value) ? value : null;
        }
    }

    private class ValueVisitorContext : IASTVisitorContext {
        private readonly RequestContext _context;

        public object? Result { get; set; }
        public CancellationToken CancellationToken => _context.CancellationToken;
        public Type ReturnType { get; }

        public ValueVisitorContext(RequestContext context, Type returnType) {
            ReturnType = returnType;
            _context = context;
        }
    }
}