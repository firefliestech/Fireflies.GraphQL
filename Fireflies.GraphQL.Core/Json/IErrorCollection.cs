namespace Fireflies.GraphQL.Core.Json;

public interface IErrorCollection {
    IGraphQLError AddError(IGraphQLPath path, string code, string message);
    IGraphQLError AddError(string code, string message);
}