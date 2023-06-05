using Fireflies.GraphQL.Abstractions.Schema;
using Fireflies.GraphQL.Core;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Demos.Server.Authors.Authors;

public class RealAuthor : IAuthor {
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    [GraphQLDeprecated("Is not populated anymore")]
    public IEnumerable<string> Emails { get; set; } = Enumerable.Empty<string>();

    public async Task<IEnumerable<IBook>> Books(ASTNode astNode, IRequestContext requestContext) {
        return await IBook.Create(this, astNode, requestContext).ConfigureAwait(false);
    }
}