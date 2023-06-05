using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Json;
using Microsoft.AspNetCore.Http;
using System.Threading.Channels;

namespace Fireflies.GraphQL.AspNet;

public class ConnectionContext : IConnectionContext {
    private readonly HttpContext _httpContext;
    private int _outstandingOperations;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Channel<(string Id, JsonWriter Writer)> _channel;

    public ConnectionContext(HttpContext httpContext) {
        _httpContext = httpContext;
        _channel = Channel.CreateUnbounded<(string Id, JsonWriter Writer)>(new UnboundedChannelOptions { SingleReader = true });
    }

    public Dictionary<string, string[]> RequestHeaders => _httpContext.Request.Headers.Where(x => !x.Key.StartsWith(":")).ToDictionary(x => x.Key, x => x.Value.ToArray());
    public string QueryString => _httpContext.Request.QueryString.Value;

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public bool IsWebSocket => WebSocket != null;
    public IWsProtocolHandler? WebSocket { get; internal set; }

    public async IAsyncEnumerator<(string Id, byte[] Result)> GetAsyncEnumerator(CancellationToken cancellationToken = new()) {
        while(!CancellationToken.IsCancellationRequested) {
            (string Id, JsonWriter Writer) entry;
            try {
                await _channel.Reader.WaitToReadAsync(CancellationToken).ConfigureAwait(false);
                entry = await _channel.Reader.ReadAsync(CancellationToken).ConfigureAwait(false);
            } catch(OperationCanceledException) {
                break;
            }

            if(IsWebSocket) {
                yield return (entry.Id, await entry.Writer.GetBuffer().ConfigureAwait(false));
            } else {
                var newOutstandingOperations = Interlocked.Decrement(ref _outstandingOperations);
                if(newOutstandingOperations != 0)
                    continue;

                yield return (entry.Id, await entry.Writer.GetBuffer().ConfigureAwait(false));
                yield break;
            }
        }
    }

    public async Task PublishResult(string? id, JsonWriter writer) {
        await _channel.Writer.WriteAsync((id, writer), CancellationToken).ConfigureAwait(false);
    }

    public void Done() {
        _cancellationTokenSource.Cancel();
    }

    public void IncreaseExpectedOperations(int i = 1) {
        if(!IsWebSocket)
            Interlocked.Add(ref _outstandingOperations, i);
    }

    public IConnectionContext CreateChildContext() {
        return new ConnectionContext(_httpContext);
    }
}