using Fireflies.GraphQL.Core.Json;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public class RequestContext : IASTVisitorContext {
    private readonly CancellationTokenSource _cancellationTokenSource;

    public IConnectionContext ConnectionContext { get; internal set; }
    public string? Id { get; }

    public RequestContext(IConnectionContext connectionContext, string? id, byte[]? rawRequest) {
        ConnectionContext = connectionContext;
        Id = id;
        RawRequest = rawRequest;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(connectionContext.CancellationToken);
    }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public byte[]? RawRequest { get; }

    public async Task PublishResult(JsonWriter writer) {
        await ConnectionContext.PublishResult(Id, writer).ConfigureAwait(false);
    }

    public void IncreaseExpectedOperations() {
        ConnectionContext.IncreaseExpectedOperations();
    }

    public void Cancel() {
        _cancellationTokenSource.Cancel();
    }
}