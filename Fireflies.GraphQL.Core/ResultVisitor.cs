using System.Collections;
using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Json;
using Fireflies.IoC.Abstractions;
using Fireflies.Utility.Reflection;
using Fireflies.Utility.Reflection.Fasterflect;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class ResultVisitor : ASTVisitor<IGraphQLContext> {
    private readonly DataJsonWriter _writer;
    private readonly FragmentAccessor _fragments;
    private readonly ValueAccessor _valueAccessor;
    private readonly IGraphQLContext _context;

    private readonly ResultContext _resultContext = new();

    private readonly IDependencyResolver _dependencyResolver;
    private readonly WrapperRegistry _wrapperRegistry;

    public ResultVisitor(object data, DataJsonWriter writer, FragmentAccessor fragments, ValueAccessor valueAccessor, IGraphQLContext context, IDependencyResolver dependencyResolver, WrapperRegistry wrapperRegistry) {
        _writer = writer;
        _fragments = fragments;
        _valueAccessor = valueAccessor;
        _context = context;
        _dependencyResolver = dependencyResolver;
        _wrapperRegistry = wrapperRegistry;
        _resultContext.Push(data.GetType(), data);
    }

    protected override async ValueTask VisitInlineFragmentAsync(GraphQLInlineFragment inlineFragment, IGraphQLContext context) {
        if(inlineFragment.TypeCondition != null) {
            var currentType = _resultContext.Peek();
            var matching = ReflectionCache.GetAllClassesThatImplements(currentType.Type).Select(x => _wrapperRegistry.GetWrapperOfSelf(x)).FirstOrDefault(x => x.Name == inlineFragment.TypeCondition.Type.Name);
            if(matching != null) {
                await base.VisitInlineFragmentAsync(inlineFragment, context).ConfigureAwait(false);
            }
        } else {
            await base.VisitInlineFragmentAsync(inlineFragment, context).ConfigureAwait(false);
        }
    }

    protected override async ValueTask VisitFieldAsync(GraphQLField field, IGraphQLContext context) {
        var parentLevel = _resultContext.Peek();

        if(parentLevel.Data == null)
            return;

        if(await RunBuiltInDirectives(field).ConfigureAwait(false))
            return;

        var memberInfo = ReflectionCache.GetMemberCache(parentLevel.Type, field.Name.StringValue);

        Type? fieldType;
        object? fieldValue;

        switch(memberInfo) {
            case PropertyInfo propertyInfo: {
                fieldType = propertyInfo.PropertyType;
                fieldValue = Reflect.PropertyGetter(propertyInfo)(parentLevel.Data);
                break;
            }
            case MethodInfo methodInfo:
                fieldType = methodInfo.ReturnType.DiscardTask();
                fieldValue = await InvokeMethod(field, methodInfo, parentLevel).ConfigureAwait(false);
                break;
            default:
                if(field.Name.StringValue == "__typename") {
                    fieldType = typeof(string);
                    fieldValue = parentLevel.Type.Name;
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

                _resultContext.Push(fieldValue.GetType(), fieldValue);

                if(isEnumerable) {
                    foreach(var data in (IEnumerable)fieldValue) {
                        _resultContext.Push(data.GetType(), data);
                        _writer.WriteStartObject();

                        foreach(var subSelection in field.SelectionSet.Selections) {
                            await VisitAsync(subSelection, context).ConfigureAwait(false);
                        }

                        _writer.WriteEndObject();
                        _resultContext.Pop();
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

                _resultContext.Pop();
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

    private async Task<object?> InvokeMethod(GraphQLField graphQLField, MethodInfo methodInfo, ResultContext.Entry parentLevel) {
        var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, methodInfo, _valueAccessor, _context, _dependencyResolver, _resultContext);
        var arguments = await argumentBuilder.Build(graphQLField).ConfigureAwait(false);
        return await ReflectionCache.ExecuteMethod(methodInfo, parentLevel.Data!, arguments).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, IGraphQLContext context) {
        await VisitAsync(await _fragments.GetFragment(fragmentSpread.FragmentName), context).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, IGraphQLContext context) {
        foreach(var selection in fragmentDefinition.SelectionSet.Selections) {
            await VisitAsync(selection, context).ConfigureAwait(false);
        }
    }
}