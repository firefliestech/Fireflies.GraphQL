using CommandLine;

namespace Fireflies.GraphQL.Client.Console.Schema;

[Verb("project-init", HelpText = "Initializes project")]
public class ProjectInitOptions {
    [Option('p', "path", Required = true, HelpText = "Path to project")]
    public string Path { get; set; }

    [Option('n', "namespace", Required = true, HelpText = "Namespace of generated client")]
    public string Namespace { get; set; }

    [Option("service-collection", Required = false, HelpText = "Generates ServiceCollection.Add{Name}Client() extension method")]
    public bool ServiceCollection { get; set; }

    [Option('f', "force", Required = false, HelpText = "Forces reinitialization")]
    public bool Force { get; set; }
}