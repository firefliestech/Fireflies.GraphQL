using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Demo;

public class StringFilterOperatorInput : GraphQLInput {
    public string? Eq { get; set; }
}