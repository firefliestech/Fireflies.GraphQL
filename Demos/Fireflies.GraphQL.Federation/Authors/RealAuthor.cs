using Fireflies.GraphQL.Abstractions.Schema;

namespace Fireflies.GraphQL.FederationDemo.Authors;

public class RealAuthor : IAuthor {
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    [GraphQLDeprecated("Is not populated anymore")]
    public IEnumerable<string> Emails { get; set; } = Enumerable.Empty<string>();
}