using System.Text.Json;
using System.Text.Json.Nodes;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Federation;

public static class FederationHelper {
    private static readonly HttpClient HttpClient = new();

    public static async Task<JsonNode?> ExecuteRequest(ASTNode astNode, ValueAccessor valueAccessor, FragmentAccessor fragmentAccessor, RequestContext requestContext, string url, OperationType operation) {
        var (query, fragments, includedVariables) = await CreateFederationQuery(astNode, requestContext, valueAccessor, fragmentAccessor).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Post, url + requestContext.ConnectionContext.QueryString);
        foreach(var item in requestContext.ConnectionContext.RequestHeaders)
            request.Headers.TryAddWithoutValidation(item.Key, item.Value);
        var buildQuery = FederationQueryBuilder.BuildQuery(query, fragments, operation, "", includedVariables);
        request.Content = new StringContent(buildQuery);

        var response = await HttpClient.SendAsync(request, requestContext.CancellationToken).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var json = await JsonSerializer.DeserializeAsync<JsonObject>(stream).ConfigureAwait(false);

        var field = (GraphQLField)astNode;

        var result = json?["data"]?[field.Name.StringValue];
        if(json?["errors"] != null)
            throw new FederationExecutionException(json["errors"]!.AsArray());

        return result;
    }

    public static async IAsyncEnumerable<JsonNode> ExecuteSubscription(ASTNode astNode, ValueAccessor valueAccessor, FragmentAccessor fragmentAccessor, RequestContext requestContext, string url, OperationType operation, string operationName) {
        var client = new FederationWebsocket(url + requestContext.ConnectionContext.QueryString, requestContext, operationName);
        await foreach(var value in client.Results().ConfigureAwait(false).WithCancellation(requestContext.CancellationToken))
            yield return value;
    }

    private static async Task<(string, string, Dictionary<string, object?>)> CreateFederationQuery(ASTNode astNode, RequestContext requestContext, ValueAccessor valueAccessor, FragmentAccessor fragmentAccessor) {
        await using var query = new StringWriter();
        var queryWriter = new QueryWriter(valueAccessor);
        await queryWriter.PrintAsync(
            astNode,
            query,
            requestContext.CancellationToken
        ).ConfigureAwait(false);

        await using var fragments = new StringWriter();
        var fragmentWriter = new SDLPrinter();
        foreach(var includedFragment in queryWriter.IncludedFragments)
            await fragmentWriter.PrintAsync(
                await fragmentAccessor.GetFragment(includedFragment).ConfigureAwait(false),
                fragments,
                requestContext.CancellationToken).ConfigureAwait(false);
        
        return (query.ToString(), fragments.ToString(), queryWriter.IncludedVariables);
    }

    private class QueryWriter : SDLPrinter {
        private readonly ValueAccessor _valueAccessor;
        public Dictionary<string, object?> IncludedVariables { get; } = new();
        public HashSet<GraphQLFragmentName> IncludedFragments { get; } = new();

        public QueryWriter(ValueAccessor valueAccessor) {
            _valueAccessor = valueAccessor;
        }

        protected override ValueTask VisitVariableAsync(GraphQLVariable variable, DefaultPrintContext context) {
            if(_valueAccessor.Variables.TryGetValue(variable.Name.StringValue, out var value))
                IncludedVariables.Add(variable.Name.StringValue, value);

            return base.VisitVariableAsync(variable, context);
        }

        protected override ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, DefaultPrintContext context) {
            IncludedFragments.Add(fragmentSpread.FragmentName);
            return base.VisitFragmentSpreadAsync(fragmentSpread, context);
        }
    }
}