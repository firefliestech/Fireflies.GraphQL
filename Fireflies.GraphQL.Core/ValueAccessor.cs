using System.Collections;
using System.Text.Json;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public class ValueAccessor {
    private readonly IRequestContext _context;
    private readonly ValueVisitor _visitor;

    internal ValueAccessor(Dictionary<string, object?>? variables, IRequestContext context) {
        _context = context;

        _visitor = new ValueVisitor(variables);
    }

    public Dictionary<string, object?> Variables => _visitor.Variables;

    public async Task<T?> GetValue<T>(ASTNode node) {
        var unconvertedValue = await GetValue(typeof(T), node).ConfigureAwait(false);
        if(unconvertedValue == null)
            return default;

        return (T)Convert.ChangeType(unconvertedValue, typeof(T));
    }

    public async Task<object?> GetValue(Type returnType, ASTNode node) {
        var visitorContext = new ValueVisitorContext(_context, returnType);
        await _visitor.VisitAsync(node, visitorContext).ConfigureAwait(false);
        var value = visitorContext.Stack.Pop();
        return value;
    }

    public async Task<object?> GetValue(Type returnType, ASTNode node, object rootObject) {
        var visitorContext = new ValueVisitorContext(_context, returnType);
        visitorContext.Stack.Push(rootObject);
        await _visitor.VisitAsync(node, visitorContext).ConfigureAwait(false);
        return Convert.ChangeType(visitorContext.Stack.Pop(), returnType);
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
            context.Stack.Push(booleanValue.BoolValue);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitStringValueAsync(GraphQLStringValue stringValue, ValueVisitorContext context) {
            context.Stack.Push(stringValue.Value.ToString());
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitNullValueAsync(GraphQLNullValue nullValue, ValueVisitorContext context) {
            context.Stack.Push(null);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitIntValueAsync(GraphQLIntValue intValue, ValueVisitorContext context) {
            context.Stack.Push(int.Parse(intValue.Value));
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitEnumValueAsync(GraphQLEnumValue enumValue, ValueVisitorContext context) {
            context.Stack.Push(Enum.Parse(context.RootElementType, enumValue.Name.StringValue, true));
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitFloatValueAsync(GraphQLFloatValue floatValue, ValueVisitorContext context) {
            context.Stack.Push(decimal.Parse(floatValue.Value));
            return ValueTask.CompletedTask;
        }

        protected override ValueTask VisitVariableAsync(GraphQLVariable variable, ValueVisitorContext context) {
            var obj = GetVariable(variable.Name.StringValue);
            if(obj != null) {
                var returnType = Nullable.GetUnderlyingType(context.RootElementType) ?? context.RootElementType;
                if(returnType.IsAssignableTo(typeof(IConvertible)))
                    context.Stack.Push(Convert.ChangeType(obj, returnType));
                else
                    context.Stack.Push(obj);
            } else {
                context.Stack.Push(null);
            }

            return ValueTask.CompletedTask;
        }

        protected override async ValueTask VisitListValueAsync(GraphQLListValue listValue, ValueVisitorContext context) {
            var list = (IList)context.Stack.Peek()!;

            var elementType = list.GetType().GenericTypeArguments[0];
            var isObject = !elementType.IsEnum && Type.GetTypeCode(elementType) == TypeCode.Object;
            var stackBefore = context.Stack.Count;
            foreach(var value in listValue.Values) {
                if(isObject)
                    context.Stack.Push(Activator.CreateInstance(elementType));

                await VisitAsync(value, context);

                if(context.Stack.Count > stackBefore) {
                    var listEntry = context.Stack.Pop();
                    list.Add(listEntry != null ? Convert.ChangeType(listEntry, elementType) : null);
                } else {
                    //TODO: Add logging, no value was added. Missing handler for graphql element?
                }
            }
        }

        protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, ValueVisitorContext context) {
            var parent = context.Stack.Peek()!;
            var propertyField = parent.GetType().GetGraphQLProperty(objectField.Name.StringValue);
            var underlyingType = Nullable.GetUnderlyingType(propertyField.PropertyType) ?? propertyField.PropertyType;

            if(Type.GetTypeCode(underlyingType) == TypeCode.Object) {
                if(underlyingType.IsCollection(out var elementType)) {
                    var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                    context.Stack.Push(list);
                } else {
                    var value = Activator.CreateInstance(underlyingType)!;
                    context.Stack.Push(value);
                }

                await VisitAsync(objectField.Value, context).ConfigureAwait(false);
                propertyField.SetValue(parent, context.Stack.Pop());
            } else {
                var value = await context.ValueAccessor.GetValue(underlyingType, objectField.Value).ConfigureAwait(false);
                propertyField.SetValue(parent, value);
            }
        }

        public object? GetVariable(string variableName) {
            return _variables.TryGetValue(variableName, out var value) ? value : null;
        }
    }

    private class ValueVisitorContext : IASTVisitorContext {
        private readonly IRequestContext _context;

        public CancellationToken CancellationToken => _context.CancellationToken;
        public Type RootElementType { get; }
        public Stack<object?> Stack { get; } = new();
        public ValueAccessor ValueAccessor => _context.ValueAccessor!;

        public ValueVisitorContext(IRequestContext context, Type rootElementType) {
            _context = context;

            if(rootElementType.IsCollection(out var elementType)) {
                var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                Stack.Push(list);
                RootElementType = elementType;
            } else {
                RootElementType = rootElementType;
            }
        }
    }
}