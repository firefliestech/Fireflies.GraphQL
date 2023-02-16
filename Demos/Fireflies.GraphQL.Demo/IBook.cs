using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Demo;

public interface IBook {
    public int BookId { get; set; }

    string Title { get; set; }
    string ISBN { get; set; }
}