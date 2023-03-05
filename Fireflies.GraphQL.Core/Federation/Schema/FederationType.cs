using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Schema;

namespace Fireflies.GraphQL.Core.Federation.Schema;

// ReSharper disable InconsistentNaming
[GraphQLNoWrapper]
public class FederationType {
    public __TypeKind Kind { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }

    public FederationField[] Fields { get; set; } = Array.Empty<FederationField>();
    public FederationType[] Interfaces { get; set; } = Array.Empty<FederationType>();
    public FederationType[] PossibleTypes { get; set; } = Array.Empty<FederationType>();

    public __EnumValue[] EnumValues { get; set; } = Array.Empty<__EnumValue>();

    public FederationInputValue[] InputFields { get; set; } = Array.Empty<FederationInputValue>();
    public FederationType? OfType { get; set; }

    public string? SpecifiedByURL { get; set; } = null;
}
// ReSharper enable InconsistentNaming