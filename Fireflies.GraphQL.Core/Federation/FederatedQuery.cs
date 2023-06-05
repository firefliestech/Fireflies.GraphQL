using Fireflies.GraphQL.Abstractions.Generator;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Federation;

[GraphQLNoWrapper]
public abstract class FederatedQuery {
    public string _query { get; set; }

    protected FederatedQuery(string query) {
        _query = query;
    }

    protected static async Task<string> CreateSelectionSet(ASTNode astNode, IRequestContext requestContext) {
        await using var query = new StringWriter();
        var sdlPrinter = new SDLPrinter();
        var hasSelectionSetNode = (IHasSelectionSetNode)astNode;
        await sdlPrinter.PrintAsync(hasSelectionSetNode.SelectionSet, query, requestContext.CancellationToken);
        return query.ToString();
    }
}