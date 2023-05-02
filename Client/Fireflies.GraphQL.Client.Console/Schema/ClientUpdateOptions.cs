using CommandLine;

namespace Fireflies.GraphQL.Client.Console.Schema;

[Verb("client-update", HelpText = "Updates the schema")]
public class ClientUpdateOptions : ISchemaOptions {
    [Option('n', "name", Required = true, HelpText = "Client name")]
    public string Name { get; set; }

    [Option('p', "path", Required = true, HelpText = "Path to project")]
    public string Path { get; set; }
}