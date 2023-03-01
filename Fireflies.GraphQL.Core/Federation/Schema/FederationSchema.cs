using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Schema;

namespace Fireflies.GraphQL.Core.Federation.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class FederationSchema {
    public string? Description { get; set; }
    public FederationType[] Types { get; set; } = Array.Empty<FederationType>();
    public FederationType? QueryType { get; set; }
    public FederationType? MutationType { get; set; }
    public FederationType? SubscriptionType { get; set; }
    public __Directive[] Directives { get; set; } = Array.Empty<__Directive>();
}