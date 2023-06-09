namespace Fireflies.GraphQL.Core.Json;

public interface IErrorCollection {
    void AddError(IGraphQLPath path, string message, string code);
    void AddError(string message, string code);
}