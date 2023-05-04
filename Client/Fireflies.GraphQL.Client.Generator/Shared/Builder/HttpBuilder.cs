public class HttpBuilder {
    private readonly HttpClient _client;

    public HttpBuilder(HttpClient client) {
        _client = client;
    }

    public void SetUri(Uri uri) {
        _client.BaseAddress = uri;
    }

    public void AddRequestHeader(string name, string value) {
        _client.DefaultRequestHeaders.Add(name, value);
    }

    public void AddRequestHeader(string name, IEnumerable<string> values) {
        _client.DefaultRequestHeaders.Add(name, values);
    }
}
