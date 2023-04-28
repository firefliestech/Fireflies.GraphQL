namespace Fireflies.GraphQL.Client.Generator.Schema;

public class SchemaField {
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public SchemaInputValue[] Args { get; set; } = Array.Empty<SchemaInputValue>();
    public SchemaType Type { get; set; } = null!;
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}