using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demos.Server.Books.Books;

public class RemoteBook : IBook {
    [GraphQLId]
    public int BookId { get; set; }

    public string ISBN { get; set; } = null!;
    public string Title { get; set; }

    public IQueryable<Edition> Editions { get; set; }
}
