namespace Fireflies.GraphQL.Core;

public class GraphQLRequest {
    public string? Query { get; set; } = null!;
    public Dictionary<string, object>? Variables { get; set; } = new();
}