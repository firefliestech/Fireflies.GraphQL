using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationEntity {
    protected readonly JObject _data;

    public FederationEntity(JObject data) {
        _data = data;
    }
}