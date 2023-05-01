using Fireflies.GraphQL.Core.Json;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;
public record ResultContext : IASTVisitorContext {
    private readonly HashSet<string> _addedFields = new();

    public Type Type { get; }
    public object? Data { get; }
    public ResultContext? ParentContext { get; }
    public CancellationToken CancellationToken => RequestContext.CancellationToken;
    public IRequestContext RequestContext { get; }
    public JsonWriter Writer { get; set; } = null!;

    public FragmentAccessor FragmentAccessor => RequestContext.FragmentAccessor!;
    public ValueAccessor ValueAccessor => RequestContext.ValueAccessor!;

    public ResultContext(Type type, IRequestContext requestContext) {
        Type = type;
        RequestContext = requestContext;
    }

    public ResultContext(object data, IRequestContext requestContext, JsonWriter writer) {
        RequestContext = requestContext;
        Writer = writer;

        Data = data;
        Type = data.GetType();
    }

    public ResultContext(Type type, object? data, ResultContext parentContext, JsonWriter writer) {
        ParentContext = parentContext;
        RequestContext = parentContext.RequestContext;
        Writer = writer;

        Type = type;
        Data = data;
    }

    public ResultContext(object data, ResultContext parentContext) {
        ParentContext = parentContext;
        RequestContext = parentContext.RequestContext;
        Writer = parentContext.Writer;

        Data = data;
        Type = data.GetType();
    }

    public bool ShouldAdd(string name) {
        return _addedFields.Add(name);
    }

    public bool Any(Type lookingFor) {
        if(Type.IsAssignableTo(lookingFor))
            return true;

        return ParentContext != null && ParentContext.Any(lookingFor);
    }
}