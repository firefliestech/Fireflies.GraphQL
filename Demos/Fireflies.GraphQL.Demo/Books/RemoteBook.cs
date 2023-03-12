namespace Fireflies.GraphQL.Demo.Books;

public class RemoteBook : IBook {
    public int BookId { get; set; }
    public string Title { get; set; } = null!;
    public string ISBN { get; set; } = null!;
}