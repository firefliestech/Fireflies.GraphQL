using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo;

public interface IBook {
    [GraphQlId]
    public int BookId { get; set; }

    string Title { get; set; }
    string ISBN { get; set; }
}
