namespace Fireflies.GraphQL.Abstractions.Authorization;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public abstract class GraphQLAuthorizationAttribute : GraphQLAuthorizationBaseAttribute {
    public abstract Task<bool> Authorize();
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public abstract class GraphQLAuthorizationAttribute<TData> : GraphQLAuthorizationBaseAttribute {
    public abstract Task<bool> Authorize(TData data);
}