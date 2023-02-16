using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.FederationDemo;

[GraphQLUnion]
public interface IAuthor {
    [GraphQlId]
    public int Id { get; set; }

    [GraphQLDescription("The authors name")]
    public string Name { get; set; }
}

public class RealAuthor : IAuthor {
    [GraphQlId]
    public int Id { get; set; }

    public string Name { get; set; }

    [GraphQLDeprecated("Is not populated anymore")]
    public IEnumerable<string> Emails { get; set; }
}

public class PseudnonymAuthor : IAuthor {
    [GraphQlId]
    public int Id { get; set; }

    public string Name { get; set; }
}