public class ClientError : IClientError {
    public string Message { get; }
    public string? Code { get; }
    public IReadOnlyList<object>? Path { get; }
    public IReadOnlyList<Location>? Locations { get; }
    public Exception? Exception { get; }
    public IReadOnlyDictionary<string, object?>? Extensions { get; }
}