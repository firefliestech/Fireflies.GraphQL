namespace Fireflies.GraphQL.Core.Json;

public class GraphQLError : IGraphQLError {
    public string Message { get; }
    public IGraphQLPath? Path { get; }
    public Dictionary<string, string> Extensions { get; } = new();

    public GraphQLError(string message, string code) {
        Message = message;
        AddExtension("code", code);
    }

    public GraphQLError(IGraphQLPath path, string message, string code) : this(message, code) {
        Path = path;
    }

    public void AddExtension(string key, string value) {
        Extensions[key] = value;
    }
}