namespace Fireflies.GraphQL.Core;

public class GraphQLRequest {
    public string Query { get; set; }
    public string OperationName { get; set; }
    public Dictionary<string, object>? Variables { get; set; }
}