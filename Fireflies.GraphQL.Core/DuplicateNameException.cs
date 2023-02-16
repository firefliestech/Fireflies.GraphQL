namespace Fireflies.GraphQL.Core;

public class DuplicateNameException : GraphQLException {
    public DuplicateNameException(string? message) : base(message) {
    }
}