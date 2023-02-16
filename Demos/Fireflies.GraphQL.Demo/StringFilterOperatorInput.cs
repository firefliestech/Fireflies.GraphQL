using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo;

public class StringFilterOperatorInput : GraphQLInput {
    public string? Eq { get; set; }
}