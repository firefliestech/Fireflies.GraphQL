namespace Fireflies.GraphQL.Client.Generator;

public class GeneratorSettings {
    public string Namespace { get; set; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Namespace);
}