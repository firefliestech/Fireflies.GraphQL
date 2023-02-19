namespace Fireflies.GraphQL.Demo;

public class BookFilterInput {
    public string? Title { get; set; }
    public StringFilterOperatorInput? ISBN { get; set; }
}