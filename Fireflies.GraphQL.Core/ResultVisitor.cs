using System.Collections;
using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Json;
using Fireflies.IoC.Abstractions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class ResultVisitor : ASTVisitor<IGraphQLContext> {
    private readonly DataJsonWriter _writer;
    private readonly FragmentAccessor _fragments;
    private readonly ValueAccessor _valueAccessor;
    private readonly IGraphQLContext _context;

    private readonly Stack<Level> _stack = new();
    private readonly IDependencyResolver _dependencyResolver;
    private readonly WrapperRegistry _wrapperRegistry;

    public ResultVisitor(object data, DataJsonWriter writer, FragmentAccessor fragments, ValueAccessor valueAccessor, IGraphQLContext context, IDependencyResolver dependencyResolver, WrapperRegistry wrapperRegistry) {
        _writer = writer;
        _fragments = fragments;
        _valueAccessor = valueAccessor;
        _context = context;
        _dependencyResolver = dependencyResolver;
        _wrapperRegistry = wrapperRegistry;
        _stack.Push(new Level(data, 0));
    }

    protected override async ValueTask VisitInlineFragmentAsync(GraphQLInlineFragment inlineFragment, IGraphQLContext context) {
        if(inlineFragment.TypeCondition != null) {
            var currentType = _stack.Peek();
            var matching = currentType.Data!.GetType().GetAllClassesThatImplements().Select(x => _wrapperRegistry.GetWrapperOfSelf(x)).FirstOrDefault(x => x.Name == inlineFragment.TypeCondition.Type.Name);
            if(matching != null) {
                await base.VisitInlineFragmentAsync(inlineFragment, context).ConfigureAwait(false);
            }
        } else {
            await base.VisitInlineFragmentAsync(inlineFragment, context).ConfigureAwait(false);
        }
    }

    protected override async ValueTask VisitFieldAsync(GraphQLField field, IGraphQLContext context) {
        var parentLevel = _stack.Peek();
        
        if(parentLevel.Data == null)
            return;

        if(await RunBuiltInDirectives(field).ConfigureAwait(false))
            return;

        var type = parentLevel.Data.GetType();
        var memberInfo = type.GetMember(field.Name.StringValue, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase).FirstOrDefault();

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
                fieldValue = await InvokeMethod(field, methodInfo, parentLevel).ConfigureAwait(false);
                break;
            default:
                if(field.Name.StringValue == "__typename") {
                    fieldType = typeof(string);
                    fieldValue = type.Name;
                } else {
                    throw new NotImplementedException();
                }

                break;
        }

        var isEnumerable = fieldType.IsAssignableTo(typeof(IEnumerable));
        var fieldName = field.Alias?.Name.StringValue ?? field.Name.StringValue;

        if(!parentLevel.ShouldAdd(fieldName))
            return;

        if(field.SelectionSet == null) {
            var isCollection = fieldType.IsCollection(out var elementType);
            var elementTypeCode = Type.GetTypeCode(elementType);

            if(fieldValue == null) {
                _writer.WriteNull(fieldName);
            } else if(isCollection) {
                _writer.WriteStartArray(fieldName);
                foreach(var value in (IEnumerable)fieldValue)
                    _writer.WriteValue(value, elementTypeCode, elementType);
                _writer.WriteEndArray();
            } else {
                _writer.WriteValue(fieldName, fieldValue, elementTypeCode, elementType);
            }
        } else {
            if(fieldValue == null) {
                _writer.WriteNull(fieldName);
            } else {
                if(isEnumerable)
                    _writer.WriteStartArray(fieldName);
                else
                    _writer.WriteStartObject(fieldName);

                var localLevel = new Level(fieldValue, parentLevel.SubLevel + 1);
                _stack.Push(localLevel);

                if(isEnumerable) {
                    foreach(var data in (IEnumerable)fieldValue) {
                        var arrayLevel = new Level(data, localLevel.SubLevel + 1);
                        _stack.Push(arrayLevel);
                        _writer.WriteStartObject();

                        foreach(var subSelection in field.SelectionSet.Selections) {
                            await VisitAsync(subSelection, context).ConfigureAwait(false);
                        }

                        _writer.WriteEndObject();
                        _stack.Pop();
                    }
                } else {
                    foreach(var subSelection in field.SelectionSet.Selections) {
                        await VisitAsync(subSelection, context).ConfigureAwait(false);
                    }
                }

                if(isEnumerable)
                    _writer.WriteEndArray();
                else
                    _writer.WriteEndObject();

                _stack.Pop();
            }
        }
    }

    private async Task<bool> RunBuiltInDirectives(GraphQLField field) {
        foreach(var directive in field.Directives ?? Enumerable.Empty<GraphQLDirective>()) {
            if(directive.Name == "skip" && directive.Arguments?[0].Name == "if") {
                var result = await _valueAccessor.GetValue<bool>(directive.Arguments[0].Value).ConfigureAwait(false);
                if(result)
                    return true;
            } else if(directive.Name == "include" && directive.Arguments?[0].Name == "if") {
                var result = await _valueAccessor.GetValue<bool>(directive.Arguments[0].Value).ConfigureAwait(false);
                if(!result)
                    return true;
            } else {
                throw new InvalidOperationException("Unknown directive");
            }
        }

        return false;
    }

    private async Task<object?> InvokeMethod(GraphQLField graphQLField, MethodInfo methodInfo, Level parentLevel) {
        var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, methodInfo, _valueAccessor, _context, _dependencyResolver);
        var arguments = await argumentBuilder.Build(graphQLField).ConfigureAwait(false);
        return await methodInfo.ExecuteMethod(parentLevel.Data!, arguments).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, IGraphQLContext context) {
        await VisitAsync(await _fragments.GetFragment(fragmentSpread.FragmentName), context).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, IGraphQLContext context) {
        foreach(var selection in fragmentDefinition.SelectionSet.Selections) {
            await VisitAsync(selection, context).ConfigureAwait(false);
        }
    }

    private class Level {
        private readonly HashSet<string> _addedFields = new();

        public object? Data { get; }
        public int SubLevel { get; }

        public Level(object data, int subLevel) {
            SubLevel = subLevel;
            Data = data;
        }

        public bool ShouldAdd(string name) {
            return _addedFields.Add(name);
        }
    }
}