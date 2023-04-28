namespace Fireflies.GraphQL.Client.Generator.Schema;

public class SchemaEnumValue {
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}