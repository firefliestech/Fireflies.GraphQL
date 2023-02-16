using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core;

public interface IASTNodeHandler {
    public ASTNode GraphQLNode { get; set; }
}