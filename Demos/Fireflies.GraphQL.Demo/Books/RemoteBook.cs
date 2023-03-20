using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo.Books;

public class RemoteBook : IBook {
    [GraphQLId]
    public int BookId { get; set; }
    public string Title { get; set; } = null!;
    public string ISBN { get; set; } = null!;
}