using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.FederationDemo;

public class PseudnonymAuthor : IAuthor {
    [GraphQlId]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}