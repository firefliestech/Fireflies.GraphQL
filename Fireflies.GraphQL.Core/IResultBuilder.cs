using Fireflies.GraphQL.Core.Json;

namespace Fireflies.GraphQL.Core;

public interface IResultBuilder : IAsyncEnumerable<(string? Id, byte[] Result)> {
    Task PublishResult(string? id, JsonWriter writer);
    void Done();
    void IncreaseExpectedOperations(int i = 1);
}