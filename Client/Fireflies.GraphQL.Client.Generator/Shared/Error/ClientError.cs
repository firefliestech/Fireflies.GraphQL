public class ClientError : IClientError {
    public string? Message { get; set; }
    public string? Code => Extensions?["code"]?.ToString();
    public IReadOnlyList<object>? Path { get; set; }
    public IReadOnlyList<Location>? Locations { get; set; }
    public IReadOnlyDictionary<string, object?>? Extensions { get; set; }
}