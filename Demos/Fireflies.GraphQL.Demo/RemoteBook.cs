namespace Fireflies.GraphQL.Demo;

public class RemoteBook : IBook {
    public int BookId { get; set; }
    public string Title { get; set; }
    public string ISBN { get; set; }
}