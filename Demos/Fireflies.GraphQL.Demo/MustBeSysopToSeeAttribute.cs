using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Demo;

public class MustBeSysopToSeeAttribute : GraphQLAuthorizationAttribute {
    internal MustBeSysopToSeeAttribute() {
    }

    public MustBeSysopToSeeAttribute(User user) {
    }

    public override Task<bool> Authorize() {
        return Task.FromResult(false);
    }

    public override string Help => "Must be authenticated as a sysop";
}