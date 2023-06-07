using Fireflies.GraphQL.Core.Json;
using Fireflies.IoC.Abstractions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public interface IRequestContext : IASTVisitorContext {
    IConnectionContext ConnectionContext { get; }
    IDependencyResolver DependencyResolver { get; }

    string? Id { get; }
    byte[]? RawRequest { get; }
    GraphQLDocument? Document { get; }

    FragmentAccessor? FragmentAccessor { get; }
    ValueAccessor? ValueAccessor { get; }

    ResultJsonWriter? Writer { get; }

    Task PublishResult(JsonWriter writer);
    void IncreaseExpectedOperations();
    void Cancel();
}