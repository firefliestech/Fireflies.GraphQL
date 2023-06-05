using Fireflies.GraphQL.Core.Json;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public interface IConnectionContext : IASTVisitorContext, IAsyncEnumerable<(string Id, byte[] Result)> {
    bool IsWebSocket { get; }
    IWsProtocolHandler? WebSocket { get; }

    public Dictionary<string, string[]> RequestHeaders { get; }
    public string QueryString { get; }

    void IncreaseExpectedOperations(int i = 1);
    Task PublishResult(string? id, JsonWriter writer);
    void Done();

    IConnectionContext CreateChildContext();
}