using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationBase : IASTNodeHandler {
    protected readonly IGraphQLContext GraphQLContext;
    public ASTNode GraphQLNode { get; set; } = null!;

    public FederationBase(IGraphQLContext context) {
        GraphQLContext = context;
    }
}