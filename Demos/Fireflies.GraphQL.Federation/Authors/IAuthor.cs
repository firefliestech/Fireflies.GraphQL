using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Schema;

namespace Fireflies.GraphQL.FederationDemo.Authors;

public interface IAuthor {
    [GraphQLId(true)]
    public int Id { get; set; }

    [GraphQLDescription("The authors name")]
    public string Name { get; set; }
}