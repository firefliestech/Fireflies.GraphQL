namespace Fireflies.GraphQL.Abstractions.Authorization;

public abstract class GraphQLAuthorizationBaseAttribute : GraphQLAttribute {
    public abstract string Help { get; }
}