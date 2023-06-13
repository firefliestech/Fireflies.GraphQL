public interface IClientError {
    string Message { get; }
    string? Code { get; }
    IReadOnlyList<object>? Path { get; }
    IReadOnlyList<Location>? Locations { get; }
    IReadOnlyDictionary<string, object?>? Extensions { get; }
}