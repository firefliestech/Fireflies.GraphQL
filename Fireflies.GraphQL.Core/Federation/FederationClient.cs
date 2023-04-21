using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Core.Federation.Schema;
using Fireflies.GraphQL.Core.Json;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

internal class FederationClient {
    private readonly string _url;
    private readonly HttpClient _httpClient;

    public FederationClient(string url) {
        _url = url;
        _httpClient = new HttpClient();
    }

    public async Task<FederationSchema> FetchSchema() {
        var stringContent = new StringContent(FederationQueryBuilder.BuildQuery(FederationQueryBuilder.SchemaQuery, null, OperationType.Query, "IntrospectionQuery", null));
        var result = await _httpClient.PostAsync(_url, stringContent).ConfigureAwait(false);
        result.EnsureSuccessStatusCode();

        var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var deserializeObject = await JsonSerializer.DeserializeAsync<JsonObject>(stream).ConfigureAwait(false);

        if(deserializeObject?["data"]?["__schema"] == null)
            throw new FederationException("Invalid schema received");

        return deserializeObject["data"]!["__schema"]!.Deserialize<FederationSchema>(DefaultJsonSerializerSettings.DefaultSettings)!;
    }
}