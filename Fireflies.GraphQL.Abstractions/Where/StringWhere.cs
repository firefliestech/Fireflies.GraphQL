namespace Fireflies.GraphQL.Abstractions.Where;

public class StringWhere : Where<string> {
    public string? Contains { get; set; }
    public string? DoesntContain { get; set; }
    public string? StartsWith { get; set; }
    public string? DoesntStartWith { get; set; }
    public string? EndsWith { get; set; }
    public string? DoesntEndWith { get; set; }
}