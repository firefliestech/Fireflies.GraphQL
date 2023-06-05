using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Schema;
using Fireflies.GraphQL.Core;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Demos.Server.Authors.Authors;

public interface IAuthor {
    [GraphQLId(true)]
    public int Id { get; set; }

    [GraphQLDescription("The authors name")]
    public string Name { get; set; }

    public Task<IEnumerable<IBook>> Books(ASTNode astNode, IRequestContext requestContext);
}