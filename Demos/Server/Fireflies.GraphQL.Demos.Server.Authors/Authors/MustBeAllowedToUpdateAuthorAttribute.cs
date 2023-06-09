using Fireflies.GraphQL.Abstractions.Authorization;

namespace Fireflies.GraphQL.Demos.Server.Authors.Authors;

public class MustBeAllowedToUpdateAuthorAttribute : GraphQLAuthorizationAttribute<UpdateAuthorInput> {
    // The name of the variable must match the name in the operation
    public override Task<bool> Authorize(UpdateAuthorInput? input) {
        return Task.FromResult(true);
    }

    public override string Help => "Need Update role";
}