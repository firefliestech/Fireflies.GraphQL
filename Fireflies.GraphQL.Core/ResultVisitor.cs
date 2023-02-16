using System.Collections;
using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core;

internal class ResultVisitor : ASTVisitor<GraphQLContext> {
    private readonly FragmentAccessor _fragments;
    private readonly VariableAccessor _variableAccessor;
    private readonly GraphQLContext _context;

    private readonly Stack<Level> _stack = new();

    public ResultVisitor(object data, JObject subResult, FragmentAccessor fragments, VariableAccessor variableAccessor, GraphQLContext context) {
        _fragments = fragments;
        _variableAccessor = variableAccessor;
        _context = context;
        _stack.Push(new Level(data, subResult, 0));
    }

    protected override async ValueTask VisitFieldAsync(GraphQLField field, GraphQLContext context) {
        var parentLevel = _stack.Peek();
        if(parentLevel.Data == null)
            return;

        if(await RunBuiltInDirectives(field))
            return;

        var type = parentLevel.Data.GetType();
        var memberInfo = type.GetMember(field.Name.StringValue, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase).First();

        Type? fieldType;
        object? fieldValue;

        switch(memberInfo) {
            case PropertyInfo propertyInfo: {
                fieldType = propertyInfo.PropertyType;
                fieldValue = propertyInfo.GetValue(parentLevel.Data, Array.Empty<object>());
                break;
            }
            case MethodInfo methodInfo:
                fieldType = methodInfo.DiscardTaskFromReturnType();
                fieldValue = await InvokeMethod(field, methodInfo, parentLevel);
                break;
            default:
                throw new NotImplementedException();
        }

        var isEnumerable = fieldType.IsAssignableTo(typeof(IEnumerable));
        var fieldName = field.Alias?.Name.StringValue ?? field.Name.StringValue;
        if(field.SelectionSet == null) {
            if(fieldType.IsEnumerable(out _)) {
                parentLevel.Add(fieldName, new JArray(fieldValue));
            } else {
                parentLevel.Add(fieldName, new JValue(fieldValue));
            }
        } else {
            if(fieldValue == null) {
                parentLevel.Add(fieldName, null);
            } else {
                var localLevel = new Level(fieldValue, isEnumerable ? new JArray() : new JObject(), parentLevel.SubLevel + 1);
                parentLevel.Add(fieldName, localLevel.Result);
                _stack.Push(localLevel);

                if(isEnumerable) {
                    foreach(var data in (IEnumerable)fieldValue) {
                        var arrayLevel = new Level(data, new JObject(), localLevel.SubLevel + 1);
                        _stack.Push(arrayLevel);
                        localLevel.Result.Add(arrayLevel.Result);

                        foreach(var subSelection in field.SelectionSet.Selections) {
                            await VisitAsync(subSelection, context);
                        }

                        _stack.Pop();
                    }
                } else {
                    foreach(var subSelection in field.SelectionSet.Selections) {
                        await VisitAsync(subSelection, context);
                    }
                }

                _stack.Pop();
            }
        }
    }

    private async Task<bool> RunBuiltInDirectives(GraphQLField field) {
        foreach(var directive in field.Directives ?? Enumerable.Empty<GraphQLDirective>()) {
            if(directive.Name == "skip" && directive.Arguments?[0].Name == "if") {
                var result = await _variableAccessor.GetValue<bool>(directive.Arguments[0].Value);
                if(result)
                    return true;
            } else if(directive.Name == "include" && directive.Arguments?[0].Name == "if") {
                var result = await _variableAccessor.GetValue<bool>(directive.Arguments[0].Value);
                if(!result)
                    return true;
            } else {
                throw new InvalidOperationException("Unknown directive");
            }
        }

        return false;
    }

    private async Task<object?> InvokeMethod(GraphQLField graphQLField, MethodInfo methodInfo, Level parentLevel) {
        var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, methodInfo, _variableAccessor, _context);
        var arguments = await argumentBuilder.Build();
        return await methodInfo.ExecuteMethod(parentLevel.Data!, arguments);
    }

    protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, GraphQLContext context) {
        await VisitAsync(await _fragments.GetFragment(fragmentSpread.FragmentName), context);
    }

    protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, GraphQLContext context) {
        foreach(var selection in fragmentDefinition.SelectionSet.Selections) {
            await VisitAsync(selection, context);
        }
    }

    private class Level {
        public object? Data { get; }
        public JContainer Result { get; }
        public int SubLevel { get; }

        public Level(object data, JContainer result, int subLevel) {
            SubLevel = subLevel;
            Data = data;
            Result = result;
        }

        public void Add(string fieldName, JToken? value) {
            switch(Result) {
                case JObject jObject:
                    jObject.Add(fieldName, value);
                    return;
                case JArray jArray:
                    jArray.Add(value);
                    return;
            }

            throw new NotImplementedException();
        }
    }
}