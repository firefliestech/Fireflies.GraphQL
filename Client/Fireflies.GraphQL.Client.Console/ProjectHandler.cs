using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Client.Console; 

public class ProjectHandler {
    public async Task<ResultCode> Init(ProjectInitOptions options) {
        ConsoleLogger.WriteInfo($"Initializing project\r\n- Namespace. {options.Namespace}\r\n- Path: {options.Path}\r\n");

        var rootPath = Path.Combine(options.Path, "GraphQL");
        if(!Directory.Exists(rootPath)) {
            Directory.CreateDirectory(rootPath);
        } else if(!options.Force) {
            ConsoleLogger.WriteError($"Project is already initialized. Use --force to reinitialize");
            return ResultCode.AlreadyInitialized;
        }

        var generatorSettingsFile = Path.Combine(rootPath, "Settings.json");
        var generatorSettings = new JsonObject {
            ["namespace"] = options.Namespace
        };
        await File.WriteAllTextAsync(generatorSettingsFile, generatorSettings.ToJsonString());

        return ResultCode.Success;
    }
}