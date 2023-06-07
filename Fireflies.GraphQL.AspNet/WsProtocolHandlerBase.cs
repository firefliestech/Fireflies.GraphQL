using System.Net.WebSockets;
using Fireflies.GraphQL.Core;
using Fireflies.Logging.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Fireflies.GraphQL.AspNet;

internal abstract class WsProtocolHandlerBase {
    private readonly HttpContext _httpContext;
    private readonly GraphQLEngine _engine;
    private readonly IFirefliesLogger _logger;
    private readonly IConnectionContext _connectionContext;
    protected WebSocket? _webSocket;

    protected WsProtocolHandlerBase(HttpContext httpContext, GraphQLEngine engine, IConnectionContext connectionConnectionContext, IFirefliesLogger logger) {
        _httpContext = httpContext;
        _engine = engine;
        _logger = logger;
        _connectionContext = connectionConnectionContext;
    }

    public async void Process() {
        _logger.Debug($"Connection from {_httpContext.Connection.RemoteIpAddress}:{_httpContext.Connection.RemotePort} to {_httpContext.Request.Path} opened");

        while(true) {
            try {
                var (webSocketReceiveResult, bytes) = await ReceiveFullMessage(_connectionContext.CancellationToken).ConfigureAwait(false);
                if(webSocketReceiveResult.MessageType == WebSocketMessageType.Close) {
                    await _webSocket!.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
                    break;
                }

                await ProcessInbound(bytes).ConfigureAwait(false);
            } catch(WebSocketException wsex) {
                break;
            } catch(Exception ex) {
                _logger.Error(ex, $"Error while running OnReceived connection. Path='{_httpContext.Request.Path}'");
                break;
            }
        }

        _logger.Debug($"Connection from {_httpContext.Connection.RemoteIpAddress}:{_httpContext.Connection.RemotePort} to {_httpContext.Request.Path} closed");
        _connectionContext.Results.Done();
    }

    protected abstract Task ProcessInbound(byte[] bytes);

    private async Task<(WebSocketReceiveResult, byte[])> ReceiveFullMessage(CancellationToken cancelToken) {
        WebSocketReceiveResult response;
        var message = new List<byte>();

        var buffer = new byte[4096];
        do {
            response = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), cancelToken).ConfigureAwait(false);
            message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
        } while(!response.EndOfMessage && response.MessageType != WebSocketMessageType.Close);

        return (response, message.ToArray());
    }

    public async Task SendAsync(ArraySegment<byte> arraySegment, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) {
        await _webSocket!.SendAsync(arraySegment, messageType, endOfMessage, cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) {
        await _webSocket!.CloseAsync(closeStatus, statusDescription, cancellationToken).ConfigureAwait(false);
    }
}