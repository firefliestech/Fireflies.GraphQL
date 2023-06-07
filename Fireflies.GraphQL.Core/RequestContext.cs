using Fireflies.GraphQL.Core.Json;
using Fireflies.IoC.Abstractions;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core;

public class RequestContext : IRequestContext {
    private readonly CancellationTokenSource _cancellationTokenSource;

    public IConnectionContext ConnectionContext { get; internal set; }
    public IDependencyResolver DependencyResolver { get; }
    public string? Id { get; }
    public byte[]? RawRequest { get; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public FragmentAccessor? FragmentAccessor { get; set; } = null!;
    public ValueAccessor? ValueAccessor { get; set; } = null!;
    public ResultJsonWriter? Writer { get; set; }
    public GraphQLDocument? Document { get; set; }

    public RequestContext(IConnectionContext connectionContext, IDependencyResolver requestLifetimeScope) {
        ConnectionContext = connectionContext;
        DependencyResolver = requestLifetimeScope;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(connectionContext.CancellationToken);
    }

    public RequestContext(IConnectionContext connectionContext, IDependencyResolver requestLifetimeScope, string? id, byte[]? rawRequest) : this(connectionContext, requestLifetimeScope){
        Id = id;
        RawRequest = rawRequest;
    }

    public async Task PublishResult(JsonWriter writer) {
        await ConnectionContext.Results.PublishResult(Id, writer).ConfigureAwait(false);
    }

    public void IncreaseExpectedOperations() {
        ConnectionContext.Results.IncreaseExpectedOperations();
    }

    public void Cancel() {
        _cancellationTokenSource.Cancel();
    }
}