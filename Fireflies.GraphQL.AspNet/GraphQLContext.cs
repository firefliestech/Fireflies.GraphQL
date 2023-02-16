using System.Collections.Concurrent;
using System.Net.WebSockets;
using Fireflies.GraphQL.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.AspNet;

public class GraphQLContext : IGraphQLContext {
    private readonly HttpContext _httpContext;
    private int _outstandingOperations;

    private readonly BlockingCollection<JObject> _results = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public GraphQLContext(HttpContext httpContext) {
        _httpContext = httpContext;
    }

    public Dictionary<string, string[]> RequestHeaders => _httpContext.Request.Headers.Where(x => !x.Key.StartsWith(":")).ToDictionary(x => x.Key, x => x.Value.ToArray());

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public bool IsWebSocket => WebSocket != null;
    public WebSocket? WebSocket { get; set; }

    public async IAsyncEnumerator<JObject> GetAsyncEnumerator(CancellationToken cancellationToken = new()) {
        foreach(var obj in _results.GetConsumingEnumerable(cancellationToken)) {
            yield return obj;

            if(IsWebSocket)
                continue;

            var newOutstandingOperations = Interlocked.Decrement(ref _outstandingOperations);
            if(newOutstandingOperations == 0) {
                _results.Dispose();
                yield break;
            }
        }
    }

    public void PublishResult(JObject result) {
        _results.Add(result);
    }

    public void Done() {
        _cancellationTokenSource.Cancel();
    }

    public void IncreaseExpectedOperations(int i = 1) {
        if(!IsWebSocket)
            Interlocked.Add(ref _outstandingOperations, i);
    }
}