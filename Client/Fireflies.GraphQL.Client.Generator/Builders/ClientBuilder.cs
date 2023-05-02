using System.Text;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class ClientBuilder : ITypeBuilder {
    private readonly StringBuilder _stringBuilder = new();

    public ClientBuilder(string clientName) {
        var className = $"{clientName}Client";

        _stringBuilder.AppendLine($"public class {className} {{");

        _stringBuilder.AppendLine("\tprivate Uri _uri;");
        _stringBuilder.AppendLine("\tprivate GraphQLWsClient _wsClient;");
        _stringBuilder.AppendLine("\tprivate JsonSerializerOptions _serializerSettings = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };");
        _stringBuilder.AppendLine();
        _stringBuilder.AppendLine("\tprivate static readonly HttpClient Client = new();");
        _stringBuilder.AppendLine();
        _stringBuilder.AppendLine($"\tpublic {className}(Uri uri, Uri subscriptionUri) {{");
        _stringBuilder.AppendLine($"\t\t_uri = uri;");
        _stringBuilder.AppendLine($"\t\t_wsClient = new GraphQLWsClient(subscriptionUri);");
        _stringBuilder.AppendLine("\t}");

        _stringBuilder.AppendLine();

        _stringBuilder.AppendLine("\tpublic void AddDefaultRequestHeader(string name, string? value) {");
        _stringBuilder.AppendLine("\t\tClient.DefaultRequestHeaders.Add(name, value);");
        _stringBuilder.AppendLine("\t}");

        _stringBuilder.AppendLine();

        _stringBuilder.AppendLine("\tpublic void AddDefaultRequestHeader(string name, IEnumerable<string?> value) {");
        _stringBuilder.AppendLine("\t\tClient.DefaultRequestHeaders.Add(name, value);");
        _stringBuilder.AppendLine("\t}");
    }

    public async Task AddOperation(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        _stringBuilder.AppendLine();

        if(operationDefinition.Operation is OperationType.Query or OperationType.Mutation)
            await GenerateRequestOperation(operationDefinition, context);
        else
            await GenerateSubscriptionOperation(operationDefinition, context);
    }

    private async Task GenerateSubscriptionOperation(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        var className = $"{operationDefinition.Name}Result";
        _stringBuilder.Append($"\tpublic GraphQLSubscriber<I{className}> {operationDefinition.Name}(");
        GenerateParameters(operationDefinition);
        _stringBuilder.AppendLine(") {");

        await GenerateRequest(operationDefinition, context);

        _stringBuilder.AppendLine($"\t\treturn _wsClient.CreateSubscriber<I{className}>(request, payload => new {className}(payload[\"errors\"], payload[\"data\"], _serializerSettings));");

        _stringBuilder.AppendLine("\t}");

        var resultTypeBuilder = context.RootContext.GetOperationResultTypeBuilder(className, operationDefinition, context);
        await resultTypeBuilder.Build();
    }

    private async Task GenerateRequestOperation(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        var className = $"{operationDefinition.Name}Result";
        _stringBuilder.Append($"\tpublic async Task<I{className}> {operationDefinition.Name}(");
        GenerateParameters(operationDefinition);
        _stringBuilder.AppendLine(") {");

        await GenerateRequest(operationDefinition, context);

        _stringBuilder.AppendLine($"\t\tvar json = await Execute(request);");
        _stringBuilder.AppendLine($"\t\treturn new {className}(json[\"errors\"], json[\"data\"], _serializerSettings);");

        _stringBuilder.AppendLine("\t}");

        var resultTypeBuilder = context.RootContext.GetOperationResultTypeBuilder(className, operationDefinition, context);
        await resultTypeBuilder.Build();
    }

    private async Task GenerateRequest(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        var visitor = new QueryCreator(context.Document);
        await visitor.Execute(operationDefinition, context);

        _stringBuilder.AppendLine("\t\tvar request = new JsonObject();");
        _stringBuilder.AppendLine($"\t\trequest[\"query\"] = @\"{visitor.Query}\";");
        _stringBuilder.AppendLine();

        if(operationDefinition.Variables != null) {
            _stringBuilder.AppendLine($"\t\tvar variables = new JsonObject();");
            _stringBuilder.AppendLine($"\t\trequest[\"variables\"] = variables;");
            foreach(var variableDefinition in operationDefinition.Variables) {
                _stringBuilder.AppendLine($"\t\tvariables[\"{variableDefinition.Variable.Name.StringValue}\"] = JsonSerializer.SerializeToNode({TypeMapper.FromGraphQL(variableDefinition.Variable.Name.StringValue)}, _serializerSettings);");
            }

            _stringBuilder.AppendLine();
        }
    }

    private void GenerateParameters(GraphQLOperationDefinition operationDefinition) {
        if(operationDefinition.Variables == null)
            return;

        var first = true;
        foreach(var variable in operationDefinition.Variables) {
            if(!first)
                _stringBuilder.Append(", ");

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

            _stringBuilder.Append(TypeMapper.FromGraphQL(type.Name.StringValue));
            if(nullable)
                _stringBuilder.Append("?");
            _stringBuilder.Append(" " + variable.Variable.Name.StringValue);

            first = false;
        }
    }

    public Task Build() {
        _stringBuilder.AppendLine();
        _stringBuilder.AppendLine("\tprivate async Task<JsonNode> Execute(JsonObject request) {");
        _stringBuilder.AppendLine("\t\tvar content = new StringContent(request.ToJsonString(), Encoding.UTF8, \"text/text\");");
        _stringBuilder.AppendLine("\t\tvar result = await Client.PostAsync(_uri, content);");
        _stringBuilder.AppendLine("\t\tresult.EnsureSuccessStatusCode();");
        _stringBuilder.AppendLine("\t\treturn (await JsonSerializer.DeserializeAsync<JsonNode>(await result.Content.ReadAsStreamAsync().ConfigureAwait(false)))!;");
        _stringBuilder.AppendLine("\t}");

        _stringBuilder.AppendLine("}");

        return Task.CompletedTask;
    }

    public string Source() {
        return _stringBuilder.ToString();
    }
}