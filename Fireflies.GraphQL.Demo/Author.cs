using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Demo;

public class Author {
    [GraphQlId]
    public int Id { get; set; }
    public string Name { get; set; }
}