using Fireflies.GraphQL.Core.Json;
using Fireflies.IoC.Abstractions;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core;

public class OperationContext : IRequestContext {
    public IRequestContext RequestContext { get; }
    public OperationType OperationType { get; }

    public IConnectionContext ConnectionContext => RequestContext.ConnectionContext;
    public IDependencyResolver DependencyResolver => RequestContext.DependencyResolver;
    
    public string? Id => RequestContext.Id;
    public byte[]? RawRequest => RequestContext.RawRequest;
    public GraphQLDocument? Document => RequestContext.Document;

    public CancellationToken CancellationToken => RequestContext.CancellationToken;

    public FragmentAccessor? FragmentAccessor => RequestContext.FragmentAccessor;
    public ValueAccessor? ValueAccessor => RequestContext.ValueAccessor;
    public ResultJsonWriter? Writer => RequestContext.Writer;

    public OperationContext(IRequestContext requestContext, OperationType operationType) {
        RequestContext = requestContext;
        OperationType = operationType;
    }

    public Task PublishResult(JsonWriter writer) {
        return RequestContext.PublishResult(writer);
    }

    public void IncreaseExpectedOperations() {
        RequestContext.IncreaseExpectedOperations();
    }

    public void Cancel() {
        RequestContext.Cancel();
    }
}