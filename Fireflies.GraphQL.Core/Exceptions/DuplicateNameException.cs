namespace Fireflies.GraphQL.Core.Exceptions;

public class DuplicateNameException : GraphQLException {
    public DuplicateNameException(string? message) : base(message) {
    }
}