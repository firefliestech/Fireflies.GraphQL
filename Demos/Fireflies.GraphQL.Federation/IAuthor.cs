using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.FederationDemo;

[GraphQLUnion]
public interface IAuthor {
    [GraphQlId]
    public int Id { get; set; }

    [GraphQLDescription("The authors name")]
    public string Name { get; set; }
}