using Fireflies.GraphQL.Abstractions.Generator;

namespace Fireflies.GraphQL.Core.Federation.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class FederationField : FederationFieldBase {
    public FederationInputValue[] Args { get; set; } = Array.Empty<FederationInputValue>();

    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}