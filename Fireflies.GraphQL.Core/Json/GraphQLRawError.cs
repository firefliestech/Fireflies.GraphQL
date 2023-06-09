using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Core.Json;

public class GraphQLRawError : IGraphQLError {
    public JsonNode Node { get; }

    public GraphQLRawError(JsonNode node) {
        Node = node;
    }
}