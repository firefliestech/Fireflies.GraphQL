using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Core.Federation.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class FederationInputValue {
    public string Name { get; set; }
    public string? Description { get; set; }
    public FederationType Type { get; set; }
    public string? DefaultValue { get; set; }
}