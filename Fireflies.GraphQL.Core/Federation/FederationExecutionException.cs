using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationExecutionException<T> : FederationExecutionException {
    private readonly T? _result;

    public FederationExecutionException(JsonArray jsonNode, T? result) : base(jsonNode, "Federated request returned error") {
        _result = result;
    }
}

public class FederationExecutionException : Exception {
    public JsonArray Node { get; }

    public FederationExecutionException(JsonArray jsonNode, string? message) : base(message) {
        Node = jsonNode;
    }
}