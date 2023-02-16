using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class VariableAccessor {
    private readonly GraphQLContext _context;
    private readonly ValueVisitor _visitor;

    public VariableAccessor(Dictionary<string, object>? variables, GraphQLContext context) {
        _context = context;
        _visitor = new ValueVisitor(variables);
    }

    public async Task<T?> GetValue<T>(ASTNode node) {
        var unconvertedValue = await GetValue(node);
        if(unconvertedValue == null)
            return default;

        return (T)Convert.ChangeType(unconvertedValue, typeof(T));
    }

    public async Task<object?> GetValue(ASTNode node) {
        var visitorContext = new ValueVisitorContext(_context);
        await _visitor.VisitAsync(node, visitorContext);
        return visitorContext.Result;
    }

    private class ValueVisitor : ASTVisitor<ValueVisitorContext> {
        private readonly Dictionary<string, object>? _variables;

        public ValueVisitor(Dictionary<string, object>? variables) {
            _variables = variables;
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

        protected override ValueTask VisitVariableAsync(GraphQLVariable variable, ValueVisitorContext context) {
            if(_variables?.TryGetValue(variable.Name.StringValue, out var value) ?? false) {
                context.Result = value;
            } else {
                context.Result = null;
            }

            return ValueTask.CompletedTask;
        }
    }

    private class ValueVisitorContext : IASTVisitorContext {
        private readonly GraphQLContext _context;

        public ValueVisitorContext(GraphQLContext context) {
            _context = context;
        }

        public object? Result { get; set; }
        public CancellationToken CancellationToken => _context.CancellationToken;
    }
}