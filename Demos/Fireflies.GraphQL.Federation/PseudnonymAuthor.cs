using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.FederationDemo;

public class PseudnonymAuthor : IAuthor {
    [GraphQLId]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}