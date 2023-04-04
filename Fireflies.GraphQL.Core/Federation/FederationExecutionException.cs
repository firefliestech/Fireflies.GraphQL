using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationExecutionException : Exception {
    public JsonArray Node { get; }

    public FederationExecutionException(JsonArray jsonNode) : base("Federated call returned error") {
        Node = jsonNode;
    }
}