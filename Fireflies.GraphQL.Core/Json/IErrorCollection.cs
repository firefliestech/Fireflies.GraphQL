namespace Fireflies.GraphQL.Core.Json;

public interface IErrorCollection {
    void AddError(IGraphQLPath path, string code, string message);
    void AddError(string code, string message);
}