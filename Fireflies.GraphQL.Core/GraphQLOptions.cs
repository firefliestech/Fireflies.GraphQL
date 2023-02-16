using Fireflies.IoC.Abstractions;
using Fireflies.Logging.Abstractions;

namespace Fireflies.GraphQL.Core;

public class GraphQLOptions {
    public string Url { get; internal set; } = "/graphql";
    public IDependencyResolver DependencyResolver { get; internal set; } = null!;
    public IFirefliesLoggerFactory LoggerFactory { get; internal set; } = null!;

    internal IEnumerable<OperationDescriptor> AllOperations => QueryOperations.Union(MutationsOperations).Union(SubscriptionOperations);
    internal List<OperationDescriptor> QueryOperations { get; } = new();
    internal List<OperationDescriptor> MutationsOperations { get; } = new();
    internal List<OperationDescriptor> SubscriptionOperations { get; } = new();
}