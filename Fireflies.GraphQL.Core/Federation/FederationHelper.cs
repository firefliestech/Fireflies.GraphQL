using System.Text.Json;
using System.Text.Json.Nodes;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Federation;

public static class FederationHelper {
    private static readonly HttpClient HttpClient = new();

    public static async Task<JsonNode?> ExecuteRequest(ASTNode astNode, ValueAccessor valueAccessor, IGraphQLContext context, string url, OperationType operation) {
        var query = await CreateFederationQuery(astNode, context).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        foreach(var item in context.RequestHeaders)
            request.Headers.TryAddWithoutValidation(item.Key, item.Value);
        request.Content = new StringContent(FederationQueryBuilder.BuildQuery(query, operation, "", valueAccessor.Variables));

        var response = await HttpClient.SendAsync(request, context.CancellationToken).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var json = await JsonSerializer.DeserializeAsync<JsonObject>(stream).ConfigureAwait(false);

        var field = (GraphQLField)astNode;

        var result = json?["data"]?[field.Name.StringValue];
        if(json?["errors"] != null)
            throw new FederationExecutionException(json["errors"]!.AsArray());

        return result;
    }

    public static async IAsyncEnumerable<JsonNode> ExecuteSubscription(ASTNode astNode, ValueAccessor valueAccessor, IGraphQLContext context, string url, OperationType operation, string operationName) {
        var query = await CreateFederationQuery(astNode, context).ConfigureAwait(false);
        var client = new FederationWebsocket(FederationQueryBuilder.BuildQuery(query, operation, "", valueAccessor.Variables), url, context, operationName);
        await foreach(var value in client.Results().ConfigureAwait(false))
            yield return value;
    }

    private static async Task<string> CreateFederationQuery(ASTNode astNode, IGraphQLContext context) {
        var writer = new StringWriter();
        await new SDLPrinter().PrintAsync(astNode, writer, context.CancellationToken).ConfigureAwait(false);
        return writer.ToString();
    }
}