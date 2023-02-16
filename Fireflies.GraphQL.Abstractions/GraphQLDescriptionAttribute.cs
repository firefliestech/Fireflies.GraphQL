namespace Fireflies.GraphQL.Abstractions;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public class GraphQLDescriptionAttribute : GraphQLAttribute {
    public string Description { get; }

    public GraphQLDescriptionAttribute(string description) {
        Description = description;
    }
}