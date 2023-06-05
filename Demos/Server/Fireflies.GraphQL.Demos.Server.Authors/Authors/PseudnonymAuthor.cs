using Fireflies.GraphQL.Core;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Demos.Server.Authors.Authors;

public class PseudnonymAuthor : IAuthor {
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTimeOffset? Born { get; set; } = DateTimeOffset.MinValue;

    public async Task<IEnumerable<IBook>> Books(ASTNode astNode, IRequestContext requestContext) {
        return await IBook.Create(this, astNode, requestContext).ConfigureAwait(false);
    }
}