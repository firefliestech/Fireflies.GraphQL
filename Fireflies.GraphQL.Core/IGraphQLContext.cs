using System.Net.WebSockets;
using GraphQLParser.Visitors;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core;

public interface IGraphQLContext : IASTVisitorContext, IAsyncEnumerable<JObject> {
    bool IsWebSocket { get; }
    WebSocket? WebSocket { get; }

    public Dictionary<string, string[]> RequestHeaders { get; }

    void PublishResult(JObject result);
    void Done();
    void IncreaseExpectedOperations(int i = 1);
}