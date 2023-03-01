using System.Net.WebSockets;
using Fireflies.GraphQL.Core.Json;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public interface IGraphQLContext : IASTVisitorContext, IAsyncEnumerable<byte[]> {
    bool IsWebSocket { get; }
    WebSocket? WebSocket { get; }

    public Dictionary<string, string[]> RequestHeaders { get; }

    void PublishResult(JsonWriter writer);
    void Done();
    void IncreaseExpectedOperations(int i = 1);
}