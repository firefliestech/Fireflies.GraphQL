namespace Fireflies.GraphQL.Abstractions.Schema;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public class GraphQLDeprecatedAttribute : GraphQLAttribute {
    public string Reason { get; }

    public GraphQLDeprecatedAttribute(string reason) {
        Reason = reason;
    }
}