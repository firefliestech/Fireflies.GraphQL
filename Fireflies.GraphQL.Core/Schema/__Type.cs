namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable InconsistentNaming
public class __Type {
    private readonly IEnumerable<__Field> _fields;
    private readonly IEnumerable<__EnumValue> _enumValues;

    public __Type(IEnumerable<__Field>? fields = null, IEnumerable<__EnumValue>? enumValues = null, IEnumerable<__InputValue>? inputValues = null) {
        _fields = fields ?? Enumerable.Empty<__Field>();
        _enumValues = enumValues ?? Enumerable.Empty<__EnumValue>();
        InputFields = inputValues?.ToArray() ?? Array.Empty<__InputValue>();
    }

    public __TypeKind Kind { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }

    public Task<__Field[]> Fields(bool includeDeprecated = false) {
        return Task.FromResult(includeDeprecated ? _fields.ToArray() : _fields.Where(f => !f.IsDeprecated).ToArray());
    }

    public __Type[] Interfaces { get; set; } = Array.Empty<__Type>();
    public __Type[] PossibleTypes { get; set; }

    public Task<__EnumValue[]> EnumValues(bool includeDeprecated = false) {
        return Task.FromResult(includeDeprecated ? _enumValues.ToArray() : _enumValues.Where(f => !f.IsDeprecated).ToArray());
    }

    public __InputValue[] InputFields { get; set; }
    public __Type? OfType { get; set; }
    public string? SpecifiedByURL { get; set; }

    public override string ToString() {
        return Name + (OfType != null ? $" ({OfType.Name})" : null);
    }
}
// ReSharper enable InconsistentNaming