namespace Fireflies.GraphQL.Core.Json;

public class GraphQLError : IGraphQLError {
    public string Message { get; }
    public IGraphQLPath? Path { get; }
    public Dictionary<string, object> Extensions { get; } = new();

    public GraphQLError(string code, string message) {
        Message = message;
        AddExtension("code", code);
    }

    public GraphQLError(IGraphQLPath path, string code, string message) : this(code, message) {
        Path = path;
    }

    public void AddExtension(string key, string value) {
        Extensions[key] = value;
    }
}