using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationBase : IASTNodeHandler {
    protected readonly IGraphQLContext _context;
    public ASTNode ASTNode { get; set; } = null!;

    public FederationBase(IGraphQLContext context) {
        _context = context;
    }
}