using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class ValueAccessor {
    private readonly IGraphQLContext _context;
    private readonly ValueVisitor _visitor;

    public ValueAccessor(Dictionary<string, object>? variables, IGraphQLContext context) {
        _context = context;
        _visitor = new ValueVisitor(variables);
    }

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

        protected override ValueTask VisitIntValueAsync(GraphQLIntValue intValue, ValueVisitorContext context) {
            context.Result = int.Parse(intValue.Value);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitEnumValueAsync(GraphQLEnumValue enumValue, ValueVisitorContext context) {
            context.Result = Enum.Parse(context.ReturnType, enumValue.Name.StringValue);
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
        private readonly IGraphQLContext _context;

        public object? Result { get; set; }
        public CancellationToken CancellationToken => _context.CancellationToken;
        public Type ReturnType { get; }

        public ValueVisitorContext(IGraphQLContext context, Type returnType) {
            ReturnType = returnType;
            _context = context;
        }
    }
}