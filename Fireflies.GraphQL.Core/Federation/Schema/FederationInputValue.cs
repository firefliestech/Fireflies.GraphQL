using Fireflies.GraphQL.Abstractions.Generator;

namespace Fireflies.GraphQL.Core.Federation.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class FederationInputValue : FederationFieldBase {
    public string? DefaultValue { get; set; }
}