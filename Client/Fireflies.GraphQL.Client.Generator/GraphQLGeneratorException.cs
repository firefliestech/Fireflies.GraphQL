namespace Fireflies.GraphQL.Client.Generator;

public class GraphQLGeneratorException : Exception {
    public GraphQLGeneratorException(string message) : base(message) {
    }

    public GraphQLGeneratorException(string message, Exception innerException) : base(message, innerException) {
    }
}