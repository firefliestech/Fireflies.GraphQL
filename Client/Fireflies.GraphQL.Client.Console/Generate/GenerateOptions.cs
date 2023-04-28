using CommandLine;

namespace Fireflies.GraphQL.Client.Console.Generate;

[Verb("generate", HelpText = "Generates the client")]
public class GenerateOptions {
    [Option('p', "path", Required = true, HelpText = "Path to project")]
    public string Path { get; set; }

    [Option('f', "force", Required = false, HelpText = "Force generation even if no changes were detected")]
    public bool Force { get; set; }
}