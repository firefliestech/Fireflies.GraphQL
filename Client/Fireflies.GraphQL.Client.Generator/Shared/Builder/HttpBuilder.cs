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
