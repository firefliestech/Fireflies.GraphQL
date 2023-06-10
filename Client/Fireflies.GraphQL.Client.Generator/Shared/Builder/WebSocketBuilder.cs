public class WebSocketBuilder {
    private readonly GraphQLWsClient _client;

    public WebSocketBuilder(GraphQLWsClient client) {
        _client = client;
    }

    public TimeSpan? ReconnectDelay {
        get => _client.ReconnectDelay;
        set => _client.ReconnectDelay = value;
    }

    public Uri? Uri {
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