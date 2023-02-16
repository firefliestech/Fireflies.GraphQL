using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Demo;

public class BookFilterInput : GraphQLInput {
    public string? Title { get; set; }
    public StringFilterOperatorInput? ISBN { get; set; }
}