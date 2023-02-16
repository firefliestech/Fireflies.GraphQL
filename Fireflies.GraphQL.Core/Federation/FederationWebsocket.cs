using System.Net.WebSockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationWebsocket<T> {
    private readonly string _query;
    private readonly string _url;
    private readonly IGraphQLContext _context;
    private readonly string _operationName;
    private readonly ClientWebSocket _client;

    public FederationWebsocket(string query, string url, IGraphQLContext context, string operationName) {
        _query = query;
        _url = url;
        _context = context;
        _operationName = operationName;
        _client = new ClientWebSocket();
    }

    public async IAsyncEnumerable<T> Results() {
        try {
            await _client.ConnectAsync(new Uri(_url.Replace("http://", "ws://").Replace("https://", "wss://")), _context.CancellationToken);
        } catch(Exception ex) {
            yield break;
        }

        await _client.SendAsync(new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(_query)), WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, _context.CancellationToken);

        while(_client is { State: WebSocketState.Open }) {
            var (webSocketReceiveResult, bytes) = await ReceiveFullMessage().ConfigureAwait(false);
            if(webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                break;

            var content = System.Text.Encoding.UTF8.GetString(bytes);
            var json = JsonConvert.DeserializeObject<JObject>(content);

            var data = json["data"]?[_operationName];
            if(data == null)
                continue;

            var result = FederationHelper.GetResult<T>(data);
            if(result != null)
                yield return result;
        }
    }

    private async Task<(WebSocketReceiveResult, byte[])> ReceiveFullMessage() {
        WebSocketReceiveResult response;
        var message = new List<byte>();

        var buffer = new byte[4096];
        do {
            response = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), _context.CancellationToken).ConfigureAwait(false);
            message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
        } while(!response.EndOfMessage && response.MessageType != WebSocketMessageType.Close);

        return (response, message.ToArray());
    }
}