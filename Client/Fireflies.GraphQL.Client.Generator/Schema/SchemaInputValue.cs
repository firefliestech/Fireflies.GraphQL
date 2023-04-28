namespace Fireflies.GraphQL.Client.Generator.Schema;

public class SchemaInputValue {
    public string Name { get; set; }
    public string? Description { get; set; }
    public SchemaType Type { get; set; }
    public string? DefaultValue { get; set; }

    public SchemaInputValue(string name, string? description, SchemaType type, string? defaultValue) {
        Name = name;
        Description = description;
        Type = type;
        DefaultValue = defaultValue;
    }
}