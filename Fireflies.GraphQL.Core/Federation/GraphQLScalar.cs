namespace Fireflies.GraphQL.Core.Federation;

public class GraphQLScalar {
    public string? Value { get; }

    public GraphQLScalar(string value) {
        Value = value;
    }
}