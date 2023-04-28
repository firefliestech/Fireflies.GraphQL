using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator;

public static class ASTNodeExtensions {
    public static ICollection<ASTNode> GetSelections(this ASTNode node) {
        if(node is IHasSelectionSetNode selectionNode)
            return selectionNode.SelectionSet?.Selections ?? new List<ASTNode>();

        return new List<ASTNode>();
    }

    public static string Capitalize(this string name) {
        return char.ToUpper(name[0]) + name[1..];
    }
}