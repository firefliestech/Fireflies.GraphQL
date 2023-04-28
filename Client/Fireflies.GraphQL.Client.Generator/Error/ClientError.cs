public class ClientError : IClientError {
    public string Message { get; set; }
    public string? Code { get; set; }
    public IReadOnlyList<object>? Path { get; set; }
    public IReadOnlyList<Location>? Locations { get; set; }
    public Exception? Exception { get; set; }
    public IReadOnlyDictionary<string, object?>? Extensions { get; set; }
}