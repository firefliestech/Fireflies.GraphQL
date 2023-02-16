using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.FederationDemo;

public class IAuthor {
    [GraphQlId]
    public int Id { get; set; }
    
    [GraphQLDescription("The authors name")]
    public string Name { get; set; }

    [GraphQLDeprecated("Is not populated anymore")]
    public IEnumerable<string> Emails { get; set; }
}

public class Author : IAuthor {
    [GraphQlId]
    
    public int Id { get; set; }

    public string Name { get; set; }
    public IEnumerable<string> Emails { get; set; }
}