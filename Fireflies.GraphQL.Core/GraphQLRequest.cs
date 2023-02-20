namespace Fireflies.GraphQL.Core;

public class GraphQLRequest {
    // Deserialized by Json.NET
    public string Query { get; set; } = null!;
    public Dictionary<string, object>? Variables { get; set; } = new();
}