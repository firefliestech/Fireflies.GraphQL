using System.Net.WebSockets;
using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Core;

public interface IWsProtocolHandler {
    Task Accept();
    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken);

    void Process();
    Task HandleResult((string Id, byte[] Result) subResult);
    string? SubProtocol { get; }
    Task<JsonNode?> HandleFederatedResponse(byte[] bytes, string operationName, IRequestContext requestContext);
}