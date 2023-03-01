using System.Net.WebSockets;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Json;
using Microsoft.AspNetCore.Http;
using Nito.AsyncEx;

namespace Fireflies.GraphQL.AspNet;

public class GraphQLContext : IGraphQLContext {
    private readonly HttpContext _httpContext;
    private int _outstandingOperations;

    private readonly AsyncProducerConsumerQueue<JsonWriter> _results = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public GraphQLContext(HttpContext httpContext) {
        _httpContext = httpContext;
    }

    public Dictionary<string, string[]> RequestHeaders => _httpContext.Request.Headers.Where(x => !x.Key.StartsWith(":")).ToDictionary(x => x.Key, x => x.Value.ToArray());

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public bool IsWebSocket => WebSocket != null;
    public WebSocket? WebSocket { get; internal set; }

    public async IAsyncEnumerator<byte[]> GetAsyncEnumerator(CancellationToken cancellationToken = new()) {
        while(!CancellationToken.IsCancellationRequested) {
            var obj = await _results.DequeueAsync(CancellationToken).ConfigureAwait(false);

            if(IsWebSocket) {
                yield return await obj.GetBuffer();
                continue;
            }

            var newOutstandingOperations = Interlocked.Decrement(ref _outstandingOperations);
            if(newOutstandingOperations == 0) {
                yield return await obj.GetBuffer();
                yield break;
            }
        }
    }

    public void PublishResult(JsonWriter writer) {
        _results.Enqueue(writer, CancellationToken);
    }

    public void Done() {
        _cancellationTokenSource.Cancel();
    }

    public void IncreaseExpectedOperations(int i = 1) {
        if(!IsWebSocket)
            Interlocked.Add(ref _outstandingOperations, i);
    }
}

