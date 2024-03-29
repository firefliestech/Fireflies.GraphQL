﻿using System.Text.Json;
using System.Text.Json.Nodes;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core.Federation;

public static class FederationHelper {
    private static readonly HttpClient HttpClient = new();

    public static async Task<JsonNode?> ExecuteRequest(ASTNode astNode, ValueAccessor valueAccessor, FragmentAccessor fragmentAccessor, IRequestContext requestContext, string url, OperationType operation) {
        var (query, fragments, includedVariables) = await CreateFederationQuery(astNode, requestContext, valueAccessor, fragmentAccessor).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Post, url + requestContext.ConnectionContext.QueryString);
        foreach(var item in requestContext.ConnectionContext.RequestHeaders)
            request.Headers.TryAddWithoutValidation(item.Key, item.Value);
        var buildQuery = FederationQueryBuilder.BuildQuery(query, fragments, operation, "", includedVariables);
        request.Content = new StringContent(buildQuery);

        var response = await HttpClient.SendAsync(request, requestContext.CancellationToken).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

        var operationName = ((GraphQLField)astNode).Name.StringValue;

        var json = await JsonSerializer.DeserializeAsync<JsonObject>(stream).ConfigureAwait(false);
        return await ParseResult(json, operationName, requestContext).ConfigureAwait(false);
    }

    public static async Task<JsonNode?> ParseResult(JsonNode? json, string operationName, IRequestContext requestContext) {
        if(json == null)
            return null;

        if(json["errors"] != null)
            throw new FederationExecutionException(json["errors"]!.AsArray());

        var result = json["data"]?[operationName];
        if(result == null)
            return null;

        var hasFederated = json["_federated"]?.GetValue<bool>() ?? false;
        if(!hasFederated)
            return result;

        if(result is JsonObject obj && obj.TryGetPropertyValue("_query", out var value))
            return await PerformFederatedQuery(value!.GetValue<string>(), requestContext);

        await ResolveFederatedQueries(result, requestContext).ConfigureAwait(false);

        return result;
    }

    private static async Task<JsonNode?> ResolveFederatedQueries(JsonNode? node, IRequestContext requestContext) {
        if(node == null)
            return null;

        if(node is JsonObject obj) {
            if(obj.TryGetPropertyValue("_query", out var value)) {
                return await PerformFederatedQuery(value!.GetValue<string>(), requestContext).ConfigureAwait(false);
            }

            foreach(var subField in obj) {
                var replaceWith = await ResolveFederatedQueries(subField.Value, requestContext).ConfigureAwait(false);
                if(replaceWith != null) {
                    obj[subField.Key] = replaceWith;
                }
            }
        } else if(node is JsonArray arr) {
            for(var index = 0; index < arr.Count; index++) {
                var item = arr[(Index)index];
                var replaceWith = await ResolveFederatedQueries(item, requestContext).ConfigureAwait(false);
                if(replaceWith != null) {
                    var localIndex = index;
                    arr[localIndex] = replaceWith;
                }
            }
        }

        return null;
    }

    private static async Task<JsonNode?> PerformFederatedQuery(string query, IRequestContext requestContext) {
        var connectionContext = requestContext.ConnectionContext.CreateChildContext();

        var lifetime = connectionContext.CreateRequestContainer();
        var subEngine = lifetime.Resolve<GraphQLEngine>();
        var subContext = new RequestContext(connectionContext, lifetime);
        await subEngine.Execute(new GraphQLRequest { Query = query }, subContext).ConfigureAwait(false);

        await foreach(var x in subEngine.Results().ConfigureAwait(false)) {
            var json = (JsonObject)JsonNode.Parse(x.Result);

            if(json?["errors"] != null)
                throw new FederationExecutionException(json["errors"]!.AsArray());

            var newResult = json["data"];
            json.Remove("data");
            return newResult;
        }

        return new JsonObject();
    }

    public static async IAsyncEnumerable<JsonNode> ExecuteSubscription(ASTNode astNode, ValueAccessor valueAccessor, FragmentAccessor fragmentAccessor, IRequestContext requestContext, string url, OperationType operation, string operationName) {
        var client = new FederationWebsocket(url + requestContext.ConnectionContext.QueryString, requestContext, operationName);
        await foreach(var value in client.Results().ConfigureAwait(false).WithCancellation(requestContext.CancellationToken))
            yield return value;
    }

    private static async Task<(string, string, Dictionary<string, object?>)> CreateFederationQuery(ASTNode astNode, IRequestContext requestContext, ValueAccessor valueAccessor, FragmentAccessor fragmentAccessor) {
        await using var query = new StringWriter();
        var queryWriter = new QueryWriter(valueAccessor);
        await queryWriter.PrintAsync(astNode,
            query,
            requestContext.CancellationToken).ConfigureAwait(false);

        await using var fragments = new StringWriter();
        var fragmentWriter = new SDLPrinter();
        foreach(var includedFragment in queryWriter.IncludedFragments)
            await fragmentWriter.PrintAsync(await fragmentAccessor.GetFragment(includedFragment).ConfigureAwait(false),
                fragments,
                requestContext.CancellationToken).ConfigureAwait(false);

        return (query.ToString(), fragments.ToString(), queryWriter.IncludedVariables);
    }

    private class QueryWriter : SDLPrinter {
        private readonly ValueAccessor _valueAccessor;
        public Dictionary<string, object?> IncludedVariables { get; } = new();
        public HashSet<string> IncludedFragments { get; } = new();

        public QueryWriter(ValueAccessor valueAccessor) {
            _valueAccessor = valueAccessor;
        }

        protected override ValueTask VisitVariableAsync(GraphQLVariable variable, DefaultPrintContext context) {
            if(_valueAccessor.Variables.TryGetValue(variable.Name.StringValue, out var value))
                IncludedVariables.Add(variable.Name.StringValue, value);

            return base.VisitVariableAsync(variable, context);
        }

        protected override ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, DefaultPrintContext context) {
            IncludedFragments.Add(fragmentSpread.FragmentName.Name.StringValue);
            return base.VisitFragmentSpreadAsync(fragmentSpread, context);
        }
    }
}