namespace Fireflies.GraphQL.Core.Federation;

public class FederationBase {
    protected readonly IGraphQLContext GraphQLContext;

    public FederationBase(IGraphQLContext context) {
        GraphQLContext = context;
    }
}