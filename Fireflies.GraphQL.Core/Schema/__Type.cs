using System.Reflection;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Federation.Schema;

namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable InconsistentNaming
[GraphQLNoWrapper]
public class __Type {
    private readonly IEnumerable<__Field> _fields;
    private readonly IEnumerable<__EnumValue> _enumValues;

    public __Type(MemberInfo? baseType, IEnumerable<__Field>? fields = null, IEnumerable<__EnumValue>? enumValues = null, IEnumerable<__InputValue>? inputValues = null) {
        _fields = fields ?? Enumerable.Empty<__Field>();
        _enumValues = enumValues ?? Enumerable.Empty<__EnumValue>();
        InputFields = inputValues?.ToArray() ?? Array.Empty<__InputValue>();
        Description = baseType?.GetDescription();
    }

    private __Type(FederationType field) {
        Kind = field.Kind;
        Name = field.Name;
        Description = field.Description;
        SpecifiedByURL = field.SpecifiedByURL;

        InputFields = field.InputFields.Select(__InputValue.FromFederation).ToArray();
        Interfaces = field.Interfaces.Select(x => new __Type(x)).ToArray();
        PossibleTypes = field.PossibleTypes.Select(x => new __Type(x)).ToArray();

        if(field.OfType != null)
            OfType = new __Type(field.OfType);

        _fields = field.Fields.Select(x =>__Field.FromFederation(x)).ToArray();
        _enumValues = field.EnumValues;
    }

    public static __Type FromFederation(FederationType field) {
        return new __Type(field);
    }

    public __TypeKind Kind { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }

    public __Field[] Fields(bool includeDeprecated = false) {
        return includeDeprecated ? _fields.ToArray() : _fields.Where(f => !f.IsDeprecated).ToArray();
    }

    public __Type[] Interfaces { get; set; } = Array.Empty<__Type>();
    public __Type[] PossibleTypes { get; set; } = Array.Empty<__Type>();

    // ReSharper disable once UnusedMember.Global
    public __EnumValue[] EnumValues(bool includeDeprecated = false) {
        return includeDeprecated ? _enumValues.ToArray() : _enumValues.Where(f => !f.IsDeprecated).ToArray();
    }

    public __InputValue[] InputFields { get; set; }
    public __Type? OfType { get; set; }

    // ReSharper disable once UnusedMember.Global
    public string? SpecifiedByURL { get; set; } = null;

    public override string ToString() {
        return Name + (OfType != null ? $" ({OfType.Name})" : null);
    }
}
// ReSharper enable InconsistentNaming