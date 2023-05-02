public class GraphQLWsClient : IAsyncDisposable {
    private readonly Uri _uri;
    private ClientWebSocket? _client;
    private CancellationToken _cancellationToken;

    private TaskCompletionSource _connectionAckCompletionSource;
    private readonly ConcurrentDictionary<Guid, GraphQLSubscriber> _subscribers = new();

    public GraphQLWsClient(Uri uri) {
        _uri = uri;
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
                    Console.WriteLine($"Error in handler: {handlerException}");
                }

                result = null;
            }
        } catch(SocketClosedException) {
        } catch(OperationCanceledException) {
        } catch(Exception ex) {
            Console.WriteLine($"Exception in receive: {ex}");
            throw;
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
        if(_client != null) return;

        _client = new ClientWebSocket();
        _client.Options.AddSubProtocol("graphql-ws");
        await _client.ConnectAsync(_uri, CancellationToken.None);

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
    }
}