using System.Text;
using System.Text.Json.Nodes;

namespace Fireflies.GraphQL.Client.Console.Schema;

public class SchemaHandler {
    public async Task<ResultCode> Init(ClientInitOptions options) {
        ConsoleLogger.WriteInfo($"Initializing {options.Name}\r\n- Path: {options.Path}\r\n- Uri: {options.Uri}\r\n");

        if(!VerifyPath(options, out var returnCode))
            return returnCode;

        var rootPath = Path.Combine(options.Path, "GraphQL");
        if(!Directory.Exists(rootPath)) {
            ConsoleLogger.WriteError($"Project is not initialized. Use project-init command first");
        }

        var clientDirectory = Path.Combine(rootPath, options.Name);
        if(Directory.Exists(clientDirectory)) {
            if(!options.Force) {
                ConsoleLogger.WriteError($"Directory {clientDirectory} already exists. Use update verb");
                return ResultCode.ClientAlreadyExists;
            }
        } else {
            Directory.CreateDirectory(clientDirectory);
        }

        var clientSettingsFile = Path.Combine(clientDirectory, "Settings.json");
        var clientSettings = new JsonObject {
            ["uri"] = options.Uri
        };
        await File.WriteAllTextAsync(clientSettingsFile, clientSettings.ToJsonString());

        return await InternalDownload(options.Uri, clientDirectory);
    }

    public async Task<ResultCode> Update(ClientUpdateOptions options) {
        ConsoleLogger.WriteInfo($"Updating {options.Name}\r\n- Path: {options.Path}\r\n");

        if(!VerifyPath(options, out var returnCode))
            return returnCode;

        var rootPath = Path.Combine(options.Path, "GraphQL");
        if(!Directory.Exists(rootPath)) {
            ConsoleLogger.WriteError($"Directory {rootPath} does not exist");
            return ResultCode.GraphQLDirectoryNotFound;
        }

        var clientDirectory = Path.Combine(rootPath, options.Name);
        if(!Directory.Exists(clientDirectory)) {
            ConsoleLogger.WriteError($"Directory {clientDirectory} does not exists. Use init verb");
            return ResultCode.ClientDirectoryNotFound;
        }

        var settingsFile = Path.Combine(clientDirectory, "Settings.json");
        var settingsContent = await File.ReadAllTextAsync(settingsFile);
        var settingsJson = JsonNode.Parse(settingsContent, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        var uri = settingsJson["uri"].GetValue<string>();

        return await InternalDownload(uri, clientDirectory);
    }

    private static async Task<ResultCode> InternalDownload(string uri, string clientDirectory) {
        try {
            ConsoleLogger.WriteInfo($"Downloading schema...");
            var schemaPath = await PerformHttpCall(uri, clientDirectory);
            ConsoleLogger.WriteSuccess($"Schema downloaded and saved to {schemaPath}");
            return ResultCode.Success;
        } catch(Exception ex) {
            ConsoleLogger.WriteError(ex, "Failed to download schema");
            return ResultCode.FailedToDownloadSchema;
        }
    }

    private static async Task<string> PerformHttpCall(string uri, string clientDirectory) {
        var httpClient = new HttpClient();
        var request = new JsonObject() {
            ["query"] = SchemaQuery
        };

        var result = await httpClient.PostAsync(uri, new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"));
        result.EnsureSuccessStatusCode();

        var json = JsonNode.Parse(await result.Content.ReadAsStreamAsync().ConfigureAwait(false));
        var schema = json["data"]["__schema"].ToJsonString();
        var schemaPath = Path.Combine(clientDirectory, "Schema.json");
        await File.WriteAllTextAsync(schemaPath, schema);
        return schemaPath;
    }

    private static bool VerifyPath(ISchemaOptions options, out ResultCode returnCode) {
        returnCode = ResultCode.Success;

        if(!Directory.Exists(options.Path)) {
            ConsoleLogger.WriteError($"Directory {options.Path} does not exist");
            {
                returnCode = ResultCode.PathDoesNotExist;
                return false;
            }
        }

        if(!Directory.GetFiles(options.Path, "*.csproj").Any()) {
            ConsoleLogger.WriteError($"Directory {options.Path} does not contain a .csproj file");
            {
                returnCode = ResultCode.ProjectFileNotFound;
                return false;
            }
        }

        return true;
    }

    private const string SchemaQuery = @"
query {
__schema {
    queryType {
      name
    }
    mutationType {
      name
    }
    subscriptionType {
      name
    }
    types {
      ...FullType
    }
    directives {
      name
      description
      locations
      args {
        ...InputValue
      }
    }
  }
}

fragment FullType on __Type {
  kind
  name
  description
  fields(includeDeprecated: true) {
    name
    description
    args {
      ...InputValue
    }
    type {
      ...TypeRef
    }
    isDeprecated
    deprecationReason
  }
  inputFields {
    ...InputValue
  }
  interfaces {
    ...TypeRef
  }
  enumValues(includeDeprecated: true) {
    name
    description
    isDeprecated
    deprecationReason
  }
  possibleTypes {
    ...TypeRef
  }
}

fragment InputValue on __InputValue {
  name
  description
  type {
    ...TypeRef
  }
  defaultValue
}

fragment TypeRef on __Type {
  kind
  name
  ofType {
    kind
    name
    ofType {
      kind
      name
      ofType {
        kind
        name
        ofType {
          kind
          name
          ofType {
            kind
            name
            ofType {
              kind
              name
              ofType {
                kind
                name
              }
            }
          }
        }
      }
    }
  }
}
";
}