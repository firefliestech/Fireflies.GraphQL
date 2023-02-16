namespace Fireflies.GraphQL.Core;

public class GraphQLException : Exception {
    public GraphQLException(string? message) : base(message) {
    }
}