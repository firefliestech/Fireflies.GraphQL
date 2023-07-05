namespace Fireflies.GraphQL.Abstractions.Schema;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Field)]
public class GraphQLDescriptionAttribute : GraphQLAttribute {
    public string Description { get; }

    public GraphQLDescriptionAttribute(string description) {
        Description = description;
    }
}