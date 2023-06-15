using System.Text;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class ClientBuilder : ITypeBuilder {
    private readonly StringBuilder _classBuilder = new();
    private readonly StringBuilder _interfaceBuilder = new();

    public ClientBuilder(string clientName) {
        var className = $"{clientName}Client";

        _interfaceBuilder.AppendLine($"public interface I{className} : IGraphQLClient {{");
        _classBuilder.AppendLine($"public class {className} : I{className} {{");

        _classBuilder.AppendLine("\tprivate Action<HttpBuilder>? _httpConfigurator;");
        _classBuilder.AppendLine("\tprivate Action<WebSocketBuilder>? _webSocketConfigurator;");
        _classBuilder.AppendLine("\tprivate GraphQLWsClient _wsClient;");
        _classBuilder.AppendLine("\tprivate IGraphQLGlobalContext _globalContext;");
        _classBuilder.AppendLine("\tprivate JsonSerializerOptions _serializerSettings = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\tprivate static readonly HttpClientHandler _httpHandler = new();");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\tpublic event Action Connecting { add { _wsClient.Connecting += value; } remove { _wsClient.Connecting -= value; } }");
        _classBuilder.AppendLine("\tpublic event Action Connected { add { _wsClient.Connected += value; } remove { _wsClient.Connected -= value; } }");
        _classBuilder.AppendLine("\tpublic event Action Disconnected { add { _wsClient.Disconnected += value; } remove { _wsClient.Disconnected -= value; } }");
        _classBuilder.AppendLine("\tpublic event Action Reconnecting { add { _wsClient.Reconnecting += value; } remove { _wsClient.Reconnecting -= value; } }");
        _classBuilder.AppendLine("\tpublic event Action<Exception> Exception { add { _wsClient.Exception += value; } remove { _wsClient.Exception -= value; } }");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\tpublic event Action? RequestStarted;");
        _classBuilder.AppendLine("\tpublic event Action? RequestEnded;");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine($"\tpublic {className}(Action<HttpBuilder> httpConfigurator, Action<WebSocketBuilder> webSocketConfigurator, IGraphQLGlobalContext globalContext) {{");
        _classBuilder.AppendLine("\t\t_httpConfigurator = httpConfigurator;");
        _classBuilder.AppendLine("\t\t_webSocketConfigurator = webSocketConfigurator;");
        _classBuilder.AppendLine("\t\t_globalContext = globalContext;");
        _classBuilder.AppendLine("\t\tCreateWsClient();");
        _classBuilder.AppendLine("\t}");
        
        _classBuilder.AppendLine();

        _classBuilder.AppendLine($"\tpublic {className}({className}Config config, IGraphQLGlobalContext globalContext) {{");
        _classBuilder.AppendLine("\t\t_httpConfigurator = config.ConfigureHttp;");
        _classBuilder.AppendLine("\t\t_webSocketConfigurator = config.ConfigureWebSocket;");
        _classBuilder.AppendLine("\t\t_globalContext = globalContext;");
        _classBuilder.AppendLine("\t\tCreateWsClient();");
        _classBuilder.AppendLine("\t}");

        _classBuilder.AppendLine();

        _classBuilder.AppendLine("\tprivate void CreateWsClient() {");
        _classBuilder.AppendLine("\t\tif(_webSocketConfigurator == null)");
        _classBuilder.AppendLine("\t\t\tthrow new ArgumentException($\"{nameof(_webSocketConfigurator)} is null\", nameof(_webSocketConfigurator));");
        _classBuilder.AppendLine();

        _classBuilder.AppendLine("\t\tvar wsClient = new GraphQLWsClient();");
        _classBuilder.AppendLine("\t\t_webSocketConfigurator(new WebSocketBuilder(wsClient));");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\t\tif(wsClient.Uri == null)");
        _classBuilder.AppendLine("\t\t\tthrow new ArgumentException($\"{nameof(wsClient.Uri)} is null\", nameof(wsClient.Uri));");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\t\t_wsClient = wsClient;");
        _classBuilder.AppendLine("\t}");
    }

    public async Task AddOperation(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        _classBuilder.AppendLine();

        if(operationDefinition.Operation is OperationType.Query or OperationType.Mutation)
            await GenerateRequestOperation(operationDefinition, context);
        else
            await GenerateSubscriptionOperation(operationDefinition, context);
    }

    private async Task GenerateSubscriptionOperation(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        var className = $"{operationDefinition.Name}Result";

        _interfaceBuilder.Append($"\tGraphQLSubscriber<I{className}> {operationDefinition.Name}(");
        GenerateParameters(operationDefinition, _interfaceBuilder);
        _interfaceBuilder.AppendLine(");");

        _classBuilder.Append($"\tpublic GraphQLSubscriber<I{className}> {operationDefinition.Name}(");
        GenerateParameters(operationDefinition, _classBuilder);
        _classBuilder.AppendLine(") {");
        
        await GenerateRequest(operationDefinition, context);

        _classBuilder.AppendLine($"\t\treturn _wsClient.CreateSubscriber<I{className}>(request, payload => new {className}(payload, _serializerSettings));");

        _classBuilder.AppendLine("\t}");

        var resultTypeBuilder = context.RootContext.GetOperationResultTypeBuilder(operationDefinition.Name.StringValue, operationDefinition, context);
        await resultTypeBuilder.Build();
    }

    private async Task GenerateRequestOperation(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        var className = $"{operationDefinition.Name}Result";

        _interfaceBuilder.Append($"\tTask<I{className}> {operationDefinition.Name}(");
        GenerateParameters(operationDefinition, _interfaceBuilder);
        _interfaceBuilder.AppendLine(");");

        _classBuilder.Append($"\tpublic async Task<I{className}> {operationDefinition.Name}(");
        GenerateParameters(operationDefinition, _classBuilder);
        _classBuilder.AppendLine(") {");
        _classBuilder.AppendLine("\t\ttry {");
        _classBuilder.AppendLine("\t\t\t_globalContext.TriggerRequestStarted(this);");
        _classBuilder.AppendLine();
        await GenerateRequest(operationDefinition, context);

        _classBuilder.AppendLine($"\t\t\tvar json = await Execute(request);");
        _classBuilder.AppendLine($"\t\t\treturn new {className}(json, _serializerSettings);");

        _classBuilder.AppendLine("\t\t} finally {");
        _classBuilder.AppendLine("\t\t\t_globalContext.TriggerRequestEnded(this);");
        _classBuilder.AppendLine("\t\t}");
        _classBuilder.AppendLine("\t}");

        var resultTypeBuilder = context.RootContext.GetOperationResultTypeBuilder(operationDefinition.Name.StringValue, operationDefinition, context);
        await resultTypeBuilder.Build();
    }

    private async Task GenerateRequest(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        var visitor = new QueryCreator();
        await visitor.Execute(operationDefinition, context);

        _classBuilder.AppendLine("\t\t\tvar request = new JsonObject();");
        _classBuilder.AppendLine($"\t\t\trequest[\"query\"] = @\"{visitor.Query}\";");
        _classBuilder.AppendLine();

        if(operationDefinition.Variables != null) {
            _classBuilder.AppendLine($"\t\t\tvar variables = new JsonObject();");
            _classBuilder.AppendLine($"\t\t\trequest[\"variables\"] = variables;");
            foreach(var variableDefinition in operationDefinition.Variables) {
                _classBuilder.AppendLine($"\t\t\tvariables[\"{variableDefinition.Variable.Name.StringValue}\"] = JsonSerializer.SerializeToNode({TypeMapper.FromGraphQL(variableDefinition.Variable.Name.StringValue)}, _serializerSettings);");
            }

            _classBuilder.AppendLine();
        }
    }

    private void GenerateParameters(GraphQLOperationDefinition operationDefinition, StringBuilder typeBuilder) {
        if(operationDefinition.Variables == null)
            return;

        var first = true;
        foreach(var variable in operationDefinition.Variables) {
            if(!first)
                typeBuilder.Append(", ");

            GraphQLNamedType? type;
            var nullable = true;

            switch(variable.Type) {
                case GraphQLNonNullType nonNullType:
                    type = (GraphQLNamedType)nonNullType.Type;
                    nullable = false;
                    break;
                default:
                    type = (GraphQLNamedType)variable.Type;
                    break;
            }

            typeBuilder.Append(TypeMapper.FromGraphQL(type.Name.StringValue));
            if(nullable)
                typeBuilder.Append("?");
            typeBuilder.Append(" " + variable.Variable.Name.StringValue);

            first = false;
        }
    }

    public Task Build() {
        _interfaceBuilder.AppendLine("}");
        
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\tprivate async Task<JsonNode> Execute(JsonObject request) {");
        _classBuilder.AppendLine("\t\tvar content = new StringContent(request.ToJsonString(), Encoding.UTF8, \"text/text\");");
        _classBuilder.AppendLine("\t\tvar client = new HttpClient(_httpHandler);");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\t\tif(_httpConfigurator == null)");
        _classBuilder.AppendLine("\t\t\tthrow new ArgumentException($\"{nameof(_httpConfigurator)} is null\", nameof(_httpConfigurator));");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\t\t_httpConfigurator(new HttpBuilder(client));");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\t\tif(client.BaseAddress == null)");
        _classBuilder.AppendLine("\t\t\tthrow new ArgumentException($\"{nameof(client.BaseAddress)} is null\", nameof(client.BaseAddress));");
        _classBuilder.AppendLine();
        _classBuilder.AppendLine("\t\tvar result = await client.PostAsync(\"\", content);");
        _classBuilder.AppendLine("\t\tresult.EnsureSuccessStatusCode();");
        _classBuilder.AppendLine("\t\treturn (await JsonSerializer.DeserializeAsync<JsonNode>(await result.Content.ReadAsStreamAsync().ConfigureAwait(false)))!;");
        _classBuilder.AppendLine("\t}");

        _classBuilder.AppendLine("}");

        return Task.CompletedTask;
    }

    public string Source() {
        return $"{_interfaceBuilder}\r\n{_classBuilder}";
    }
}