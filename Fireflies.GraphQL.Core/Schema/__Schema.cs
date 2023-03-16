using Fireflies.GraphQL.Abstractions.Generator;

namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class __Schema {
    public string? Description { get; set; }
    public __Type[] Types { get; set; } = Array.Empty<__Type>();
    public __Type? QueryType { get; set; }
    public __Type? MutationType { get; set; }
    public __Type? SubscriptionType { get; set; }
    public __Directive[] Directives { get; set; } = Array.Empty<__Directive>();
}