namespace Fireflies.GraphQL.Core.Federation.Schema;

public abstract class FederationFieldBase {
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public FederationType Type { get; set; } = null!;
}