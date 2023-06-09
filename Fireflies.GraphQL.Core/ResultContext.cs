using Fireflies.GraphQL.Core.Json;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public sealed record ResultContext : IASTVisitorContext {
    private readonly HashSet<string> _addedFields = new();

    public Type Type { get; }
    public object? Data { get; private set; }
    public ResultContext? ParentContext { get; }
    public CancellationToken CancellationToken => RequestContext.CancellationToken;
    public IRequestContext RequestContext { get; }
    public JsonWriter? Writer { get; set; } = null!;

    public FragmentAccessor FragmentAccessor => RequestContext.FragmentAccessor!;
    public ValueAccessor ValueAccessor => RequestContext.ValueAccessor!;

    public Stack<object> Path { get; }

    public ResultContext(Type type, IRequestContext requestContext) {
        Type = type;
        RequestContext = requestContext;
        Writer = requestContext.Writer;
        Path = new();
    }

    public ResultContext(object data, IRequestContext requestContext, JsonWriter writer) : this(data.GetType(), requestContext) {
        Writer = writer;
        Data = data;
    }

    private ResultContext(Type type, ResultContext parentContext) {
        ParentContext = parentContext;
        RequestContext = parentContext.RequestContext;
        Writer = parentContext.Writer;
        Type = type;
        Path = new Stack<object>(parentContext.Path.Reverse());
    }

    public ResultContext CreateChildContext(object data, JsonWriter writer) {
        var childContext = new ResultContext(data.GetType(), this) {
            Data = data,
            Writer = writer
        };

        return childContext;
    }

    public ResultContext CreateChildContext(object data) {
        var childContext = new ResultContext(data.GetType(), this) {
            Data = data,
            Writer = Writer
        };

        return childContext;
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