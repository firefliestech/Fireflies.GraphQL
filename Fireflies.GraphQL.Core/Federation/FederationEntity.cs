using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationEntity {
    protected readonly JObject GraphQLData;

    public FederationEntity(JObject data) {
        GraphQLData = data;
    }
}