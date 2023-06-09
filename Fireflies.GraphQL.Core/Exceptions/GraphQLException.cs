namespace Fireflies.GraphQL.Core.Exceptions;

public class GraphQLException : Exception {
    public GraphQLException(string? message) : base(message) {
    }

    public GraphQLException(string? message, Exception innerException) : base(message, innerException) {
    }
}