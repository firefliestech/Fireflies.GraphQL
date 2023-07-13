using System.Text.Json;

namespace Fireflies.GraphQL.Core;

public class GraphQLRequest {
    public string? Query { get; set; } = null!;
    public Dictionary<string, JsonElement?>? Variables { get; set; } = new();
}