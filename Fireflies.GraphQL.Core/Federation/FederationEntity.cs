using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationEntity {
    protected readonly JsonObject GraphQLData;

    public FederationEntity(JsonObject data) {
        GraphQLData = data;
    }
}