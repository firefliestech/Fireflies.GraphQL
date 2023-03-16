using Fireflies.GraphQL.Abstractions.Authorization;

namespace Fireflies.GraphQL.Demo;

public class MustBeSalesmanAttribute : GraphQLAuthorizationAttribute {
    internal MustBeSalesmanAttribute() {
    }

    public MustBeSalesmanAttribute(User user) {
    }

    public override Task<bool> Authorize() {
        return Task.FromResult(false);
    }

    public override string Help => "Must be authenticated as a salesman";
}