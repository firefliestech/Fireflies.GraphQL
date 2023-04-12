namespace Fireflies.GraphQL.Abstractions;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Parameter)]
public class GraphQLIdAttribute : GraphQLAttribute {
    public bool KeepAsOriginalType { get; }

    public GraphQLIdAttribute() {
    }

    public GraphQLIdAttribute(bool keepAsOriginalType = false) {
        KeepAsOriginalType = keepAsOriginalType;
    }
}