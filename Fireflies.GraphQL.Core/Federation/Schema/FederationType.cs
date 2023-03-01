using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Schema;

namespace Fireflies.GraphQL.Core.Federation.Schema;

// ReSharper disable InconsistentNaming
[GraphQLNoWrapper]
public class FederationType {
    public __TypeKind Kind { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }

    public FederationField[] Fields { get; set; }
    public FederationType[] Interfaces { get; set; } = Array.Empty<FederationType>();
    public FederationType[] PossibleTypes { get; set; } = Array.Empty<FederationType>();

    public __EnumValue[] EnumValues { get; set; }

    public FederationInputValue[] InputFields { get; set; }
    public FederationType? OfType { get; set; }

    public string? SpecifiedByURL { get; set; } = null;
}
// ReSharper enable InconsistentNaming