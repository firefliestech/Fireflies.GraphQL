using CommandLine;

namespace Fireflies.GraphQL.Client.Console.Schema;

[Verb("client-init", HelpText = "Downloads the schema")]
public class ClientInitOptions : ISchemaOptions {
    [Option('n', "name", Required = true, HelpText = "Client name")]
    public string Name { get; set; }

    [Option('p', "path", Required = true, HelpText = "Path to project")]
    public string Path { get; set; }

    [Option('u', "uri", Required = true, HelpText = "Url to GraphQL service")]
    public string Uri { get; set; }

    [Option('f', "force", Required = false, HelpText = "Forces reinitialization")]
    public bool Force { get; set; }
}