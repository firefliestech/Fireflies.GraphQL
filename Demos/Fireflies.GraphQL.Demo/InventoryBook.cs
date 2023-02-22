using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Sorting;

namespace Fireflies.GraphQL.Demo;

public class InventoryBook : IBook {
    [GraphQlId]
    public int BookId { get; set; }

    public string Title { get; set; } = null!;
    public string ISBN { get; set; } = null!;

    public Task<decimal> CalculatePrice() {
        return Task.FromResult((decimal)23.3);
    }
    
    [GraphQLSort]
    public IEnumerable<Edition> Editions { get; set; }

    [MustBeSalesman]
    [GraphQLDescription("Returns the exact inventory")]
    [GraphQLDeprecated("Will be 0 from 2024-01-01")]
    public int ExactInventory { get; set; }
}