namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable once InconsistentNaming
// ReSharper disable UnusedMember.Global
public class __Directive {
    public string? Name { get; set; }
    public string? Description { get; set; }
    public __DirectiveLocation[] Locations { get; set; } = Array.Empty<__DirectiveLocation>();
    public __InputValue[] Args { get; set; } = Array.Empty<__InputValue>();
    public bool IsRepeatable { get; set; }
}
// ReSharper restore UnusedMember.Global