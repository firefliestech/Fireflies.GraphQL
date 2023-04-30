using System.Collections;
using System.Collections.Concurrent;
using System.Formats.Asn1;
using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Json;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.IoC.Abstractions;
using Fireflies.Utility.Reflection;
using Fireflies.Utility.Reflection.Fasterflect;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class ResultVisitor : ASTVisitor<ResultContext> {
    private readonly FragmentAccessor _fragments;
    private readonly ValueAccessor _valueAccessor;

    private readonly IDependencyResolver _dependencyResolver;
    private readonly WrapperRegistry _wrapperRegistry;
    private readonly ScalarRegistry _scalarRegistry;

    public ResultVisitor(FragmentAccessor fragments, ValueAccessor valueAccessor, IDependencyResolver dependencyResolver, WrapperRegistry wrapperRegistry, ScalarRegistry scalarRegistry) {
        _fragments = fragments;
        _valueAccessor = valueAccessor;
        _dependencyResolver = dependencyResolver;
        _wrapperRegistry = wrapperRegistry;
        _scalarRegistry = scalarRegistry;
    }

    protected override async ValueTask VisitInlineFragmentAsync(GraphQLInlineFragment inlineFragment, ResultContext context) {
        if(inlineFragment.TypeCondition != null) {
            var matching = ReflectionCache.GetAllClassesThatImplements(context.Type).Select(x => _wrapperRegistry.GetWrapperOfSelf(x)).FirstOrDefault(x => x.Name == inlineFragment.TypeCondition.Type.Name);
            if(matching != null) {
                await base.VisitInlineFragmentAsync(inlineFragment, context).ConfigureAwait(false);
            }
        } else {
            await base.VisitInlineFragmentAsync(inlineFragment, context).ConfigureAwait(false);
        }
    }

    protected override async ValueTask VisitFieldAsync(GraphQLField field, ResultContext context) {
        if(context.Data == null)
            return;

        if(await RunBuiltInDirectives(field).ConfigureAwait(false))
            return;

        var memberInfo = ReflectionCache.GetMemberCache(context.Type, field.Name.StringValue);

        Type? fieldType;
        object? fieldValue;

        switch(memberInfo) {
            case PropertyInfo propertyInfo: {
                fieldType = propertyInfo.PropertyType;
                fieldValue = Reflect.PropertyGetter(propertyInfo)(context.Data);
                break;
            }
            case MethodInfo methodInfo:
                fieldType = methodInfo.ReturnType.DiscardTask();
                fieldValue = await InvokeMethod(field, methodInfo, context).ConfigureAwait(false);
                break;
            default:
                if(field.Name.StringValue == "__typename") {
                    fieldType = typeof(string);
                    fieldValue = context.Type.Name;
                } else {
                    throw new NotImplementedException();
                }

                break;
        }

        var isEnumerable = fieldType.IsAssignableTo(typeof(IEnumerable));
        var fieldName = field.Alias?.Name.StringValue ?? field.Name.StringValue;

        if(!context.ShouldAdd(fieldName))
            return;

        if(field.SelectionSet == null) {
            var isCollection = fieldType.IsCollection(out var elementType);
            var elementTypeCode = Type.GetTypeCode(Nullable.GetUnderlyingType(elementType) ?? elementType);

            if(fieldValue == null) {
                context.Writer.WriteNull(fieldName);
            } else if(isCollection) {
                context.Writer.WriteStartArray(fieldName);
                foreach(var value in (IEnumerable)fieldValue)
                    context.Writer.WriteValue(value, elementTypeCode, elementType);
                context.Writer.WriteEndArray();
            } else {
                context.Writer.WriteValue(fieldName, fieldValue, elementTypeCode, elementType);
            }
        } else {
            if(fieldValue == null) {
                context.Writer.WriteNull(fieldName);
            } else {
                if(isEnumerable) {
                    context.Writer.WriteStartArray(fieldName);
                    var methodInfo = memberInfo as MethodInfo;
                    if(methodInfo != null && methodInfo.HasCustomAttribute<GraphQLParallel>(out var graphQLParallel)) {
                        await ExecuteParallel(field, context, fieldValue, graphQLParallel!).ConfigureAwait(false);
                    } else {
                        await ExecuteSynchronously(field, context, fieldValue);
                    }
                    
                    context.Writer.WriteEndArray();
                } else {
                    context.Writer.WriteStartObject(fieldName);
                    var subResultContext = new ResultContext(fieldValue, context);
                    foreach(var subSelection in field.SelectionSet.Selections) {
                        await VisitAsync(subSelection, subResultContext).ConfigureAwait(false);
                    }

                    context.Writer.WriteEndObject();
                }
            }
        }
    }

    private async Task ExecuteSynchronously(GraphQLField field, ResultContext context, object fieldValue) {
        foreach(var data in (IEnumerable)fieldValue) {
            var subContext = new ResultContext(data, context);
            context.Writer.WriteStartObject();

            foreach(var subSelection in field.SelectionSet.Selections) {
                await VisitAsync(subSelection, subContext).ConfigureAwait(false);
            }

            context.Writer.WriteEndObject();
        }
    }

    private async Task ExecuteParallel(GraphQLField field, ResultContext context, object fieldValue, GraphQLParallel parallelOptions) {
        var results = new ConcurrentDictionary<int, JsonWriter>();
        
        var values = ((IEnumerable)fieldValue).OfType<object>();
        await values.AsyncParallelForEach(async data => {
            var jsonWriter = new JsonWriter(_scalarRegistry);
            results.TryAdd(data.Index, jsonWriter);

            var subResultContext = new ResultContext(data.Value.GetType(), data.Value, context, jsonWriter);

            jsonWriter.WriteStartObject();

            foreach(var subSelection in field.SelectionSet.Selections) {
                await VisitAsync(subSelection, subResultContext).ConfigureAwait(false);
            }

            jsonWriter.WriteEndObject();
        }).ConfigureAwait(false);

        if(parallelOptions.SortResults) {
            foreach(var result in results.OrderBy(x => x.Key))
                await context.Writer.WriteRaw(result.Value).ConfigureAwait(false);
        } else {
            foreach(var result in results)
                await context.Writer.WriteRaw(result.Value).ConfigureAwait(false);
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

    private async Task<object?> InvokeMethod(GraphQLField graphQLField, MethodInfo methodInfo, ResultContext parentLevel) {
        var argumentBuilder = new ArgumentBuilder(graphQLField.Arguments, methodInfo, _valueAccessor, _fragments, parentLevel.RequestContext, _dependencyResolver, parentLevel);
        var arguments = await argumentBuilder.Build(graphQLField).ConfigureAwait(false);
        return await ReflectionCache.ExecuteMethod(methodInfo, parentLevel.Data!, arguments).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, ResultContext context) {
        await VisitAsync(await _fragments.GetFragment(fragmentSpread.FragmentName), context).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, ResultContext context) {
        foreach(var selection in fragmentDefinition.SelectionSet.Selections) {
            await VisitAsync(selection, context).ConfigureAwait(false);
        }
    }
}