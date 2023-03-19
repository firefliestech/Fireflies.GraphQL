using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Schema;

namespace Fireflies.GraphQL.FederationDemo;

[GraphQLUnion]
public interface IAuthor {
    [GraphQLId]
    public int Id { get; set; }

    [GraphQLDescription("The authors name")]
    public string Name { get; set; }
}