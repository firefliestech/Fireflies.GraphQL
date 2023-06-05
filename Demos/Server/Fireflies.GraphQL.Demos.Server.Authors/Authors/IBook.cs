using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Federation;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Demos.Server.Authors.Authors;

public class IBook : FederatedQuery {
    private IBook(string query) : base(query) {
    }

    public static async Task<IBook[]> Create(IAuthor author, ASTNode astNode, IRequestContext requestContext) {
        var query = $"query {{ books(filter: {{ authorId: {author.Id} }}) {await CreateSelectionSet(astNode, requestContext)} }}";
        return new[] { new IBook(query) };
    }
}