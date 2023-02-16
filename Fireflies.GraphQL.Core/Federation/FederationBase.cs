using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationBase : IASTNodeHandler {
    protected readonly GraphQLContext _context;
    public ASTNode ASTNode { get; set; }

    public FederationBase(GraphQLContext context) {
        _context = context;
    }
}