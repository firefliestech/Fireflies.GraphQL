using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo.Books;

public interface IBook {
    [GraphQLId]
    public int BookId { get; set; }

    string Title { get; set; }
    string ISBN { get; set; }
}
