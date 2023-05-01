using System.Net.WebSockets;
using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationWebsocket {
    private readonly string _url;
    private readonly IRequestContext _requestContext;
    private readonly string _operationName;
    private readonly ClientWebSocket _client;

    public FederationWebsocket(string url, IRequestContext requestContext, string operationName) {
        _url = url;
        _requestContext = requestContext;
        _operationName = operationName;
        _client = new ClientWebSocket();

        var headersToCopy = _requestContext.ConnectionContext.RequestHeaders.Where(x => ShouldCopyHeader(x.Key));
        foreach(var item in headersToCopy)
            _client.Options.SetRequestHeader(item.Key, string.Join(",", item));
    }

    private static bool ShouldCopyHeader(string key) {
        switch(key) {
            case "Connection":
            case "Upgrade":
            case "Host":
                return false;
            default:
                return !key.StartsWith("Sec-WebSocket");
        }
    }

    public async IAsyncEnumerable<JsonNode> Results() {
        try {
            if(_requestContext.ConnectionContext.WebSocket!.SubProtocol != null)
                _client.Options.AddSubProtocol(_requestContext.ConnectionContext.WebSocket!.SubProtocol);
            await _client.ConnectAsync(new Uri(_url.Replace("http://", "ws://").Replace("https://", "wss://")), _requestContext.CancellationToken).ConfigureAwait(false);
        } catch(Exception) {
            //TODO: Add logging
            yield break;
        }

        await _client.SendAsync(new ArraySegment<byte>(_requestContext.RawRequest), WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, _requestContext.CancellationToken).ConfigureAwait(false);

        while(_client is { State: WebSocketState.Open }) {
            JsonNode? json;
            try {
                var (webSocketReceiveResult, bytes) = await ReceiveFullMessage().ConfigureAwait(false);
                if(webSocketReceiveResult.MessageType == WebSocketMessageType.Close) {
                    await _client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", _requestContext.CancellationToken);
                    break;
                }

                json = _requestContext.ConnectionContext.WebSocket.HandleFederatedResponse(bytes, _operationName);
            } catch(OperationCanceledException) {
                break;
            }

            yield return json;
        }

        try {
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        } catch {
        }

        try {
            _client.Dispose();
        } catch {
        }
    }

    private async Task<(WebSocketReceiveResult, byte[])> ReceiveFullMessage() {
        WebSocketReceiveResult response;
        var message = new List<byte>();

        var buffer = new byte[4096];
        do {
            response = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), _requestContext.CancellationToken).ConfigureAwait(false);
            message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
        } while(!response.EndOfMessage && response.MessageType != WebSocketMessageType.Close);

        return (response, message.ToArray());
    }
}