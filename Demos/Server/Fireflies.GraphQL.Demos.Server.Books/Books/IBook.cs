using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demos.Server.Books.Books;

public interface IBook {
    [GraphQLId]
    public int BookId { get; }

    string ISBN { get; }

    string Title { get; }
}
