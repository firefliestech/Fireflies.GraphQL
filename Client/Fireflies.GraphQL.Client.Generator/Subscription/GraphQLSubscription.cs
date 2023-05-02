public class GraphQLSubscription<T> : IAsyncDisposable {
    private readonly Action<T> _onMessage;
    private readonly GraphQLSubscriber<T> _subscriber;

    public GraphQLSubscription(Action<T> onMessage, GraphQLSubscriber<T> subscriber) {
        _onMessage = onMessage;
        _subscriber = subscriber;
    }

    public void Handle(T message) {
        _onMessage(message);
    }

    public async ValueTask DisposeAsync() {
        await _subscriber.Unwatch(this);
    }
}