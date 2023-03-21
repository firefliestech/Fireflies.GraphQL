using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Federation;

public static class FederationHelper {
    private static readonly MethodInfo EnumerableMethod;
    private static readonly HttpClient HttpClient = new();

    static FederationHelper() {
        EnumerableMethod = typeof(FederationHelper).GetMethod(nameof(GetEnumerableResult), BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    public static async Task<T?> ExecuteRequest<T>(ASTNode astNode, IGraphQLContext context, string url, OperationType operation) {
        var query = await CreateFederationQuery(astNode, context).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        foreach(var item in context.RequestHeaders)
            request.Headers.TryAddWithoutValidation(item.Key, item.Value);
        request.Content = new StringContent(FederationQueryBuilder.BuildQuery(query, operation, ""));

        var result = await HttpClient.SendAsync(request, context.CancellationToken).ConfigureAwait(false);
        var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var json = await JsonSerializer.DeserializeAsync<JsonObject>(stream).ConfigureAwait(false);
        
        var field = (GraphQLField)astNode;

        return GetResult<T>(json?["data"]?[field.Name.StringValue]);
    }

    public static async IAsyncEnumerable<T> ExecuteSubscription<T>(ASTNode astNode, IGraphQLContext context, string url, OperationType operation, string operationName) {
        var query = await CreateFederationQuery(astNode, context).ConfigureAwait(false);
        var client = new FederationWebsocket<T>(FederationQueryBuilder.BuildQuery(query, operation, ""), url, context, operationName);
        await foreach(var value in client.Results().ConfigureAwait(false))
            yield return value;
    }

    private static async Task<string> CreateFederationQuery(ASTNode astNode, IGraphQLContext context) {
        var writer = new StringWriter();
        await new FederationSDLPrinter().PrintAsync(astNode, writer, context.CancellationToken).ConfigureAwait(false);
        return writer.ToString();
    }

    public static T? GetField<T>(JsonNode? data, string field) {
        if(data == null)
            return default;

        return GetResult<T>(data[field]);
    }

    public static T? GetResult<T>(JsonNode? token) {
        if(token == null)
            return default;

        if(typeof(T).IsCollection(out var elementType)) {
            return (T)EnumerableMethod.MakeGenericMethod(elementType).Invoke(null, new object[] { token })!;
        }

        return CreateInstance<T>(token);
    }

    private static IEnumerable<T?> GetEnumerableResult<T>(JsonNode token) {
        return token.AsArray().Select(CreateInstance<T>);
    }

    private static T? CreateInstance<T>(JsonNode? token) {
        if(token == null)
            return default;

        var type = typeof(T);
        if(type.IsEnum) {
            var value = token.GetValue<JsonElement>();
            if(value.ValueKind == JsonValueKind.String)
                return (T)Enum.Parse(type, token.GetValue<string>());

            return (T)Convert.ChangeType(token.GetValue<int>(), typeof(T));
        }

        switch(Type.GetTypeCode(type)) {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Byte:
            case TypeCode.SByte:
                return token.GetValue<T>()!;

            case TypeCode.Boolean:
                return token.GetValue<T>()!;

            case TypeCode.Char:
            case TypeCode.String:
                return token.GetValue<T>()!;

            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return token.GetValue<T>()!;

            default:
                if(type.IsSubclassOf(typeof(GraphQLId))) {
                    return (T)(object)new GraphQLId<string>(token.GetValue<string>());
                }

                if(type.IsSubclassOf(typeof(GraphQLScalar))) {
                    return (T)Activator.CreateInstance(type, token.GetValue<string>())!;
                }

                var typename = token["__typename"]?.GetValue<string>();
                if(typename != null) {
                    var assembly = type.Assembly;
                    var implementation = assembly.GetType(typename)!;
                    return (T)Activator.CreateInstance(implementation, token)!;
                }

                return (T)Activator.CreateInstance(type, token)!;
        }
    }

    public class FederationSDLPrinter : SDLPrinter {
        protected override async ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, DefaultPrintContext context) {
            if(selectionSet.Selections.Any(x => x.Kind == ASTNodeKind.Field && ((GraphQLField)x).Name.StringValue == "__typename")) {
                await base.VisitSelectionSetAsync(selectionSet, context).ConfigureAwait(false);
                return;
            }

            var graphQLField = new GraphQLField {
                Name = new GraphQLName("__typename")
            };
            selectionSet.Selections.Add(graphQLField);
            await base.VisitSelectionSetAsync(selectionSet, context).ConfigureAwait(false);
            selectionSet.Selections.Remove(graphQLField);
        }
    }
}