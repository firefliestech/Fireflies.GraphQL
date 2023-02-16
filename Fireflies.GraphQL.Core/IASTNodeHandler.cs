using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core;

public interface IASTNodeHandler {
    public ASTNode ASTNode { get; set; }
}