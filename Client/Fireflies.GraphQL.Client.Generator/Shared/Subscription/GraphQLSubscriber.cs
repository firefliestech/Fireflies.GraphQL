public abstract class GraphQLSubscriber {
    public Guid Id { get; } = Guid.NewGuid();

    public abstract Task Restart();
    public abstract void Handle(JsonNode payload);
}

public class GraphQLSubscriber<T> : GraphQLSubscriber {
    private readonly GraphQLWsClient _client;
    private readonly Func<JsonNode, T> _instanceCreator;
    private readonly List<GraphQLSubscription<T>> _watchers = new();

    public JsonNode Request { get; private set; }

    public GraphQLSubscriber(GraphQLWsClient client, JsonNode request, Func<JsonNode, T> instanceCreator) {
        _client = client;
        _instanceCreator = instanceCreator;
        Request = request;
    }

    public override async Task Restart() {
        Request = Request.Deserialize<JsonNode>();
        await _client.Start(this);
    }
    
    public async Task<IAsyncDisposable> Watch(Action<T> onMessage) {
        var subscription = new GraphQLSubscription<T>(onMessage, this);
        _watchers.Add(subscription);

        if(_watchers.Count == 1) {
            await _client.Start(this);
        }

        return subscription;
    }

    public async Task Unwatch(GraphQLSubscription<T> subscription) {
        _watchers.Remove(subscription);

        if(_watchers.Count == 0)
            await _client.Stop(this);
    }

    public override void Handle(JsonNode payload) {
        var message = _instanceCreator(payload);
        foreach(var watcher in _watchers) {
            watcher.Handle(message);
        }
    }
}