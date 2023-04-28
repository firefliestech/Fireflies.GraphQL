namespace Fireflies.GraphQL.Client.Generator.Schema;

public class SchemaType {
    public SchemaTypeKind Kind { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }

    public SchemaField[] Fields { get; set; } = Array.Empty<SchemaField>();

    public SchemaType[] Interfaces { get; set; } = Array.Empty<SchemaType>();
    public SchemaType[] PossibleTypes { get; set; } = Array.Empty<SchemaType>();
    public SchemaEnumValue[] EnumValues { get; set; } = Array.Empty<SchemaEnumValue>();

    public SchemaInputValue[] InputFields { get; set; }
    public SchemaType? OfType { get; set; }

    public string? SpecifiedByURL { get; set; } = null;

    protected bool Equals(SchemaType other) {
        return Name == other.Name && Equals(OfType, other.OfType);
    }

    public override bool Equals(object? obj) {
        if(ReferenceEquals(null, obj)) return false;
        if(ReferenceEquals(this, obj)) return true;
        if(obj.GetType() != this.GetType()) return false;

        return Equals((SchemaType)obj);
    }

    public override int GetHashCode() {
        unchecked {
            return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (OfType != null ? OfType.GetHashCode() : 0);
        }
    }

    public override string ToString() {
        return Name ?? "<NULL>";
    }
}