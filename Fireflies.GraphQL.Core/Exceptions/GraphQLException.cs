namespace Fireflies.GraphQL.Core.Exceptions;

public class GraphQLException : Exception {
    public GraphQLException(string? message) : base(message) {
    }
}