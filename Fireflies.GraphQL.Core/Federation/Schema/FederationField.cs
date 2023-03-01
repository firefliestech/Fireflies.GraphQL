using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Core.Federation.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class FederationField {
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public FederationInputValue[] Args { get; set; } = Array.Empty<FederationInputValue>();
    public FederationType Type { get; set; } = null!;
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}