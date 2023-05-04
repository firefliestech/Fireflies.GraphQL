public class WebSocketBuilder {
    private readonly GraphQLWsClient _client;

    public WebSocketBuilder(GraphQLWsClient client) {
        _client = client;
    }

    public void SetUri(Uri uri) {
        _client.Uri = uri;
    }
}
