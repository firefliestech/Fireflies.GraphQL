namespace Fireflies.GraphQL.Abstractions;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public abstract class GraphQLAuthorizationAttribute : GraphQLAttribute {
    public abstract Task<bool> Authorize();
    public abstract string Help { get; }
}