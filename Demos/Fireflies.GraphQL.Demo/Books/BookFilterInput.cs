namespace Fireflies.GraphQL.Demo.Books;

public class BookFilterInput {
    public string? Title { get; set; }
    public StringFilterOperatorInput? ISBN { get; set; }
}