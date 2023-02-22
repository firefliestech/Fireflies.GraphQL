using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class __InputValue {
    public string Name { get; set; }
    public string? Description { get; set; }
    public __Type Type { get; set; }
    public string? DefaultValue { get; set; }

    public __InputValue(string name, string? description, __Type type, string? defaultValue) {
        Name = name;
        Description = description;
        Type = type;
        DefaultValue = defaultValue;
    }
}