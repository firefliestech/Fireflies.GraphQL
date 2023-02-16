namespace Fireflies.GraphQL.Contract;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public class GraphQLDescriptionAttribute : GraphQLAttribute {
    public string Description { get; set; }
}