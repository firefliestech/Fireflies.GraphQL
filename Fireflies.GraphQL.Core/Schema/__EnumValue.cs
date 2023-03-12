using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Generator;

namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class __EnumValue {
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }

    public __EnumValue(string name, string? description, string? deprecationReason) {
        Name = name;
        Description = description;
        DeprecationReason = deprecationReason;
        IsDeprecated = DeprecationReason != null;
    }
}