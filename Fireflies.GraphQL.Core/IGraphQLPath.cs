namespace Fireflies.GraphQL.Core;

public interface IGraphQLPath {
    IEnumerable<object> Path { get; }
    IGraphQLPath Add(params object[] subPath);
}