using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo;

public class InventoryBook : IBook {
    [GraphQlId]
    public int BookId { get; set; }

    public string Title { get; set; }
    public string ISBN { get; set; }

    public async Task<decimal> CalculatePrice() {
        return (decimal)23.3;
    }

    [MustBeSalesman]
    [GraphQLDescription("Returns the exact inventory")]
    [GraphQLDeprecated("Will be 0 from 2024-01-01")]
    public int ExactInventory { get; set; }
}