using Fireflies.IoC.Abstractions;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public interface IConnectionContext : IASTVisitorContext {
    bool IsWebSocket { get; }
    IWsProtocolHandler? WebSocket { get; }

    Dictionary<string, string[]> RequestHeaders { get; }
    string QueryString { get; }

    IResultBuilder Results { get; }

    IConnectionContext CreateChildContext();
    IDependencyResolver CreateRequestContainer();
}

public interface IConnectionContext<out THttpContext> : IConnectionContext {
    public THttpContext HttpContext { get; }
}