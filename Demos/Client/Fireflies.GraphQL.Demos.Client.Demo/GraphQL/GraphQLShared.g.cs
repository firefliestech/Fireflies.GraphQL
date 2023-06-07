// <auto-generated/>
// <generated-at="2023-05-15T05:26:59.115+00:00"/>
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Demos.GraphQL;

public class HttpBuilder {
    private readonly HttpClient _client;

    public HttpBuilder(HttpClient client) {
        _client = client;
    }

    public Uri Uri {
        get {
            return _client.BaseAddress;
        }
        set {
            _client.BaseAddress = value;
        }
    }
    
    public void AddRequestHeader(string name, string value) {
        _client.DefaultRequestHeaders.Add(name, value);
    }

    public void AddRequestHeader(string name, IEnumerable<string> values) {
        _client.DefaultRequestHeaders.Add(name, values);
    }
}


public class WebSocketBuilder {
    private readonly GraphQLWsClient _client;

    public WebSocketBuilder(GraphQLWsClient client) {
        _client = client;
    }

    public TimeSpan? ReconnectDelay {
        get => _client.ReconnectDelay;
        set => _client.ReconnectDelay = value;
    }

    public Uri Uri {
        get => _client.Uri;
        set => _client.Uri = value;
    }

    public event Action Connecting {
        add {
            _client.Connecting += value;
        }
        remove {
            _client.Connecting -= value;
        }
    }

    public event Action Connected {
        add {
            _client.Connected += value;
        }
        remove {
            _client.Connected -= value;
        }
    }

    public event Action Reconnecting {
        add {
            _client.Reconnecting += value;
        }
        remove {
            _client.Reconnecting -= value;
        }
    }

    public event Action Disconnected {
        add {
            _client.Disconnected += value;
        }
        remove {
            _client.Disconnected -= value;
        }
    }
}

public class ClientError : IClientError {
    public string Message { get; set; }
    public string? Code { get; set; }
    public IReadOnlyList<object>? Path { get; set; }
    public IReadOnlyList<Location>? Locations { get; set; }
    public Exception? Exception { get; set; }
    public IReadOnlyDictionary<string, object?>? Extensions { get; set; }
}

public interface IClientError {
    string Message { get; }
    string? Code { get; }
    IReadOnlyList<object>? Path { get; }
    IReadOnlyList<Location>? Locations { get; }
    Exception? Exception { get; }
    IReadOnlyDictionary<string, object?>? Extensions { get; }
}

public readonly struct Location {
    public Location(int line, int column) {
        if(line < 1) {
            throw new ArgumentOutOfRangeException(nameof(line), line, "Line location out of range");
        }

        if(column < 1) {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column location out of range");
        }

        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}

public interface IOperationResult<T> {
    IEnumerable<IClientError> Errors { get; }
    T Data { get; }
}

public class ConnectionNotAcceptedException : SubscriptionException {
}

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

public class GraphQLWsClient : IAsyncDisposable {
    private ClientWebSocket? _client;
    private CancellationToken _cancellationToken;

    private TaskCompletionSource _connectionAckCompletionSource;
    private readonly ConcurrentDictionary<Guid, GraphQLSubscriber> _subscribers = new();

    public Uri Uri { get; set; }
    public TimeSpan? ReconnectDelay;

    public event Action? Connecting;
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action? Reconnecting;
    public event Action<Exception>? Exception;

    public GraphQLWsClient() {
    }

    public GraphQLSubscriber<TInterface> CreateSubscriber<TInterface>(JsonObject request, Func<JsonNode, TInterface> instanceFactory) {
        var subscriber = new GraphQLSubscriber<TInterface>(this, request, instanceFactory);

        _subscribers.TryAdd(subscriber.Id, subscriber);

        return subscriber;
    }

    private async Task Send(JsonObject payload) {
        var json = payload.ToJsonString();
        await _client!.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, _cancellationToken);
    }

    public async Task Receive() {
        try {
            var buffer = new ArraySegment<byte>(new byte[1024]);
            var result = "";
            while(!_cancellationToken.IsCancellationRequested) {
                var received = await _client!.ReceiveAsync(buffer, _cancellationToken);

                if(received.MessageType == WebSocketMessageType.Close) {
                    throw new SocketClosedException();
                }

                result += Encoding.UTF8.GetString(buffer.Array, 0, received.Count);

                if(!received.EndOfMessage)
                    continue;

                try {
                    var json = JsonSerializer.Deserialize<JsonNode>(result);
                    await MessageReceived(json);
                } catch(Exception handlerException) {
                    Exception?.Invoke(handlerException);
                }

                result = null;
            }
        } catch(SocketClosedException) {
        } catch(OperationCanceledException) {
        } catch(Exception ex) {
            await DisposeAsync();
            Exception?.Invoke(ex);

            if(_subscribers.Any() && ReconnectDelay != null) {
                await Task.Delay(ReconnectDelay.Value);
                Task.Run(async () => await Reconnect());
            }
        }
    }

    private async Task Reconnect() {
        Reconnecting?.Invoke();
        await EnsureClient();

        if(_client != null) {
            foreach(var subscriber in _subscribers)
                await subscriber.Value.Restart();
        }
    }

    private async Task MessageReceived(JsonNode json) {
        switch(json["type"].GetValue<string>()) {
            case "connection_ack":
                // To avoid deadlock
                Task.Run(() => _connectionAckCompletionSource.SetResult());
                break;
            case "data": {
                var id = json["id"].GetValue<Guid>();
                if(_subscribers.TryGetValue(id, out var subscriber)) {
                    var payload = json["payload"];
                    subscriber.Handle(payload);
                }

                break;
            }
            case "ping": {
                await Send(new JsonObject {
                    ["type"] = "pong"
                });
                break;
            }
        }
    }

    private async Task SendConnectionInit() {
        var connectionInit = new JsonObject {
            ["type"] = "connection_init"
        };

        await Send(connectionInit);
    }

    public async Task Start<T>(GraphQLSubscriber<T> subscriber) {
        await EnsureClient();

        var payload = new JsonObject {
            ["type"] = "start",
            ["id"] = subscriber.Id,
            ["payload"] = subscriber.Request
        };

        await Send(payload);
    }

    public async Task Stop<T>(GraphQLSubscriber<T> subscriber) {
        var payload = new JsonObject {
            ["type"] = "stop",
            ["id"] = subscriber.Id
        };

        await Send(payload);

        _subscribers.TryRemove(subscriber.Id, out _);

        if(!_subscribers.Any())
            await DisposeAsync();
    }

    private async Task EnsureClient() {
        if(_client != null)
            return;

        while(!_cancellationToken.IsCancellationRequested) {
            try {
                Connecting?.Invoke();

                _client = new ClientWebSocket();
                _client.Options.AddSubProtocol("graphql-ws");
                await _client.ConnectAsync(Uri, CancellationToken.None);

                _connectionAckCompletionSource = new TaskCompletionSource();

                Task.Run(async () => await Receive());
                await SendConnectionInit();

                var winner = await Task.WhenAny(new[] {
                    _connectionAckCompletionSource.Task, Task.Delay(5000, _cancellationToken)
                });

                if(winner != _connectionAckCompletionSource.Task) {
                    await DisposeAsync();
                    throw new ConnectionNotAcceptedException();
                }

                Connected?.Invoke();
                return;
            } catch(Exception ex) {
                Exception?.Invoke(ex);
                await DisposeAsync();

                if(ReconnectDelay != null)
                    Task.Delay(ReconnectDelay.Value);
                else
                    throw;
            }
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await _client?.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _cancellationToken);
        } catch {
        }

        try {
            _client?.Dispose();
        } catch {
        }

        _client = null;

        Disconnected?.Invoke();
    }
}

public class SocketClosedException : SubscriptionException {
}

public class SubscriptionException : Exception {
}