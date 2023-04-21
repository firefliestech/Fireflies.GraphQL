namespace Fireflies.GraphQL.Core.Federation;

public class FederationBase {
    protected readonly IConnectionContext ConnectionContext;

    public FederationBase(IConnectionContext context) {
        ConnectionContext = context;
    }
}