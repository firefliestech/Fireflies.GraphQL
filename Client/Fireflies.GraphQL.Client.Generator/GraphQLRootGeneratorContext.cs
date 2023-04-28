using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Fireflies.GraphQL.Client.Generator.Builders;
using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Client.Generator;

public class GraphQLRootGeneratorContext : IASTVisitorContext {
    private readonly HashSet<string> _generatedTypes = new();
    private readonly List<ITypeBuilder> _typeBuilders = new();

    public Dictionary<string?, SchemaType> SchemaTypes { get; }
    public CancellationToken CancellationToken { get; }

    public string? SubscriptionType { get; set; }
    public string? MutationType { get; set; }
    public string? QueryType { get; set; }

    public string Source => string.Join("\r\n", _typeBuilders.Select(x => x.Source()));

    public GraphQLRootGeneratorContext(JsonNode schema) {
        QueryType = schema["queryType"]?["name"]?.GetValue<string>();
        MutationType = schema["mutationType"]?["name"]?.GetValue<string>();
        SubscriptionType = schema["subscriptionType"]?["name"]?.GetValue<string>();
        SchemaTypes = schema["types"].Deserialize<IEnumerable<SchemaType>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } }).ToDictionary(x => x.Name);
    }

    public bool ShouldGenerateType(string type) {
        return _generatedTypes.Add(type);
    }

    public RawTypeBuilder GetRawTypeBuilder() {
        var builder = new RawTypeBuilder();
        _typeBuilders.Add(builder);
        return builder;
    }

    public ClientBuilder GetClientBuilder(string clientName) {
        var builder = new ClientBuilder(clientName);
        _typeBuilders.Add(builder);
        return builder;
    }

    public OperationResultTypeBuilder GetOperationResultTypeBuilder(string typeName, GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        var schemaType = GetSchemaType(operationDefinition, context);
        var builder = new OperationResultTypeBuilder(typeName, operationDefinition, null, schemaType, context);
        _typeBuilders.Add(builder);
        return builder;
    }

    private static SchemaType GetSchemaType(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        return context.GetSchemaType(operationDefinition.Operation switch {
            OperationType.Query => context.RootContext.QueryType!,
            OperationType.Mutation => context.RootContext.MutationType!,
            OperationType.Subscription => context.RootContext.SubscriptionType!,
            _ => throw new ArgumentOutOfRangeException(nameof(operationDefinition.Operation), operationDefinition, null)
        });
    }

    public TypeBuilder GetTypeBuilder(string typeName, ASTNode astNode, GraphQLGeneratorContext context) {
        var builder = new TypeBuilder(typeName, astNode, context);
        _typeBuilders.Add(builder);
        return builder;
    }

    public SchemaType GetSchemaType(string type) {
        return SchemaTypes[type];
    }
}