public interface IClientError {
    string Message { get; }
    string? Code { get; }
    IReadOnlyList<object>? Path { get; }
    IReadOnlyList<Location>? Locations { get; }
    Exception? Exception { get; }
    IReadOnlyDictionary<string, object?>? Extensions { get; }
}