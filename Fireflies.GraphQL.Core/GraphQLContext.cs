using System.Collections.Concurrent;
using System.Net.WebSockets;
using GraphQLParser.Visitors;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core;

public class GraphQLContext : IASTVisitorContext, IAsyncEnumerable<JObject> {
    private int _outstandingOperations;

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public bool IsWebSocket => WebSocket != null;
    public WebSocket? WebSocket { get; set; }

    private readonly BlockingCollection<JObject> _results = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

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