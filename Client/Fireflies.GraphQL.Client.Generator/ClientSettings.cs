namespace Fireflies.GraphQL.Client.Generator;

public class ClientSettings {
    public string Namespace { get; set; }
    public string Uri { get; set; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Namespace) && !string.IsNullOrWhiteSpace(Uri);
}