namespace Fireflies.GraphQL.Abstractions;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Parameter)]
public class GraphQLIdAttribute : GraphQLAttribute {
}