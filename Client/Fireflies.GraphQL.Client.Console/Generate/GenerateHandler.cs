using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Client.Generator;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Console.Generate; 

public class GenerateHandler {
    public async Task<ResultCode> Generate(GenerateOptions options) {
        var rootPath = Path.Combine(options.Path, "GraphQL");

        ConsoleLogger.WriteInfo("Generating clients...\r\n");
        var success = true;

        foreach(var clientDirectory in Directory.GetDirectories(rootPath).Select(x => new DirectoryInfo(x))) {
            var clientName = clientDirectory.Name;

            ConsoleLogger.WriteInfo($"Generating {clientName}...");
            
            var settings = await GetSettings(clientDirectory);
            var graphQLDocuments = GetGraphQLDocuments(clientDirectory);
            var schema = await GetSchema(clientDirectory);

            var clientFile = new FileInfo(Path.Combine(clientDirectory.FullName, $"{clientName}Client.g.cs"));
            if(clientFile.Exists && !options.Force) {
                var lastChange = clientDirectory.GetFileSystemInfos().Where(x => !x.Name.EndsWith(".g.cs")).Max(x => x.LastWriteTimeUtc);
                var lastGenerated = await GetGeneratedTimestamp(clientFile);

                if(lastGenerated > lastChange) {
                    ConsoleLogger.WriteWarning("No files changed. Use --force option to generate anyway");
                    continue;
                }
            }

            var generator = new ClientGenerator(clientName, schema, settings ?? new ClientSettings(), graphQLDocuments);
            try {
                await generator.Generate();
                await File.WriteAllTextAsync(clientFile.FullName, generator.Source);
                ConsoleLogger.WriteSuccess($"Generated {clientName}!");
            } catch(Exception ex) {
                ConsoleLogger.WriteError(ex, $"Failed to generate {clientName}!");
                throw;
            }
        }

        return success ? ResultCode.Success : ResultCode.GenerationFailed;
    }

    private static IEnumerable<GraphQLDocument> GetGraphQLDocuments(DirectoryInfo clientDirectory) {
        var graphQLDocuments = Directory.GetFiles(clientDirectory.FullName, "*.graphql").Select(ParseGraphQLFile);
        if(!graphQLDocuments.Any())
            throw new GraphQLGeneratorException($"No *.graphql files found. Path: {clientDirectory.FullName}");

        return graphQLDocuments;
    }

    private static async Task<JsonNode?> GetSchema(DirectoryInfo clientDirectory) {
        var schemaFile = new FileInfo(Path.Combine(clientDirectory.FullName, "Schema.json"));
        if(!schemaFile.Exists)
            throw new GraphQLGeneratorException($"Schema.json file does not exist. Path: {schemaFile.FullName}");

        var schema = JsonNode.Parse(await File.ReadAllTextAsync(schemaFile.FullName));
        if(schema == null)
            throw new GraphQLGeneratorException("Schema is empty");

        return schema;
    }

    private static async Task<ClientSettings?> GetSettings(DirectoryInfo clientDirectory) {
        var settingsFile = new FileInfo(Path.Combine(clientDirectory.FullName, "Settings.json"));
        if(!settingsFile.Exists)
            throw new GraphQLGeneratorException($"Settings.json file does not exist. Path: {settingsFile.FullName}");

        var settings = JsonNode.Parse(await File.ReadAllTextAsync(settingsFile.FullName)).Deserialize<ClientSettings>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if(string.IsNullOrWhiteSpace(settings.Namespace))
            throw new GraphQLGeneratorException($"Settings.json is not valid. Namespace is required");

        return settings;
    }

    private static GraphQLDocument ParseGraphQLFile(string x) {
        try {
            return GraphQLParser.Parser.Parse(File.ReadAllText(x));
        } catch(Exception ex) {
            throw new GraphQLGeneratorException($"Failed to parse. File: {x}.", ex);
        }
    }

    private static async Task<DateTimeOffset> GetGeneratedTimestamp(FileInfo clientFile) {
        var lines = await File.ReadAllLinesAsync(clientFile.FullName);
        var lookingFor = "// <generated-at=\"";
        var generatedAtLine = lines.FirstOrDefault(x => x.StartsWith(lookingFor));
        if(generatedAtLine != null) {
            return DateTimeOffset.ParseExact(generatedAtLine[lookingFor.Length..][..^3], "yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        }

        return DateTimeOffset.MinValue;
    }
}