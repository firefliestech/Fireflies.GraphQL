using Fireflies.GraphQL.Core.Exceptions;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationException : GraphQLException {
    public FederationException(string message) : base(message) {
    }
}