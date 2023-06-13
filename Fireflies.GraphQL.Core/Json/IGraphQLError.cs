namespace Fireflies.GraphQL.Core.Json;

public interface IGraphQLError {
    string Message { get; }
    IGraphQLPath? Path { get; }
    Dictionary<string, object?> Extensions { get; }
}