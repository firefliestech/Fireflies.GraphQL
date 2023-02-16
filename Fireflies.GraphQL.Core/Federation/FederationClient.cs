using Fireflies.GraphQL.Core.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.ComponentModel;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

internal class FederationClient {
    private readonly string _url;
    private readonly HttpClient _httpClient;

    public FederationClient(string url) {
        _url = url;
        _httpClient = new HttpClient();
    }

    public async Task<__Schema?> FetchSchema() {
        var stringContent = new StringContent(FederationQueryBuilder.BuildQuery(FederationQueryBuilder.SchemaQuery, OperationType.Query, "IntrospectionQuery"));
        var result = await _httpClient.PostAsync(_url, stringContent);
        result.EnsureSuccessStatusCode();

        var readAsStringAsync = await result.Content.ReadAsStringAsync();
        var deserializeObject = JsonConvert.DeserializeObject<JObject>(readAsStringAsync);
        return deserializeObject["data"]["__schema"].ToObject<__Schema>();
    }

    private class QueryResult {
        public __Schema Data { get; set; }
    }
}

//public class XBase : FederationBase {
//    public XBase(GraphQLContext context) : base(context, "min url", "mitt namn") {
//    }

//    public Task<Author> Author(int authorId) {
//        return ExecuteRequest<Author>();
//    }
//}

//public class Author {
//}