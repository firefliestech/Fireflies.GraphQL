using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo.Books;

public interface IBook {
    [GraphQLId]
    public int BookId { get; }

    string ISBN { get; }

    string Title { get; }
}
