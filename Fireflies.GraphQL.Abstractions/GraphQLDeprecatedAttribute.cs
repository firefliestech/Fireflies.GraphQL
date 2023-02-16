namespace Fireflies.GraphQL.Abstractions;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public class GraphQLDeprecatedAttribute : GraphQLAttribute {
    public string Reason { get; }

    public GraphQLDeprecatedAttribute(string reason) {
        Reason = reason;
    }
}