using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo;

public class BookFilterInput : GraphQLInput {
    public string? Title { get; set; }
    public StringFilterOperatorInput? ISBN { get; set; }
}