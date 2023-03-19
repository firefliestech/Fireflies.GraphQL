using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Schema;

namespace Fireflies.GraphQL.FederationDemo;

public class RealAuthor : IAuthor {
    [GraphQLId]
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    [GraphQLDeprecated("Is not populated anymore")]
    public IEnumerable<string> Emails { get; set; } = Enumerable.Empty<string>();
}