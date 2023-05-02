﻿using Fireflies.GraphQL.Client.Generator.Builders;
using System.Reflection;
using System.Reflection.Emit;

namespace Fireflies.GraphQL.Client.Generator;

public class SharedGenerator {
    private readonly DirectoryInfo _rootDirectory;
    private readonly GeneratorSettings _generatorSettings;

    public SharedGenerator(DirectoryInfo rootDirectory, GeneratorSettings generatorSettings) {
        _rootDirectory = rootDirectory;
        _generatorSettings = generatorSettings;
    }

    public async Task GenerateSharedFiles() {
        var typeBuilder = new RawTypeBuilder();
        typeBuilder.AppendLine("// <auto-generated/>");
        typeBuilder.AppendLine($"// <generated-at=\"{DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz")}\"/>");

        typeBuilder.AppendLine("using System.Collections.Concurrent;");
        typeBuilder.AppendLine("using System.Net.WebSockets;");
        typeBuilder.AppendLine("using System.Text;");
        typeBuilder.AppendLine("using System.Text.Json;");
        typeBuilder.AppendLine("using System.Text.Json.Nodes;");
        
        typeBuilder.AppendLine();

        typeBuilder.AppendLine($"namespace {_generatorSettings.Namespace};");

        await GetResource("Fireflies.GraphQL.Client.Generator.Error.", typeBuilder);
        await GetResource("Fireflies.GraphQL.Client.Generator.Subscription.", typeBuilder);
        var filePath = Path.Combine(_rootDirectory.FullName, "GraphQLShared.g.cs");
        await File.WriteAllTextAsync(filePath, typeBuilder.Source());
    }

    private async Task GetResource(string resourcePattern, RawTypeBuilder builder) {
        var assembly = Assembly.GetExecutingAssembly();
        foreach(var resourceName in assembly.GetManifestResourceNames().Where(x => x.StartsWith(resourcePattern))) {
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream!);
            var content = await reader.ReadToEndAsync();
            builder.AppendLine();
            builder.AppendLine(content);
        }
    }
}
