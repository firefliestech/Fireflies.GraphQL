using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Schema;
using Fireflies.GraphQL.Abstractions.Sorting;

namespace Fireflies.GraphQL.Demo.Books;

public class InventoryBook : IBook {
    [GraphQLId]
    public int BookId { get; set; }

    public string ISBN { get; set; } = null!;
    public string Title { get; set; } = null!;

    public Task<decimal> CalculatedPrice() {
        return Task.FromResult(23.3M);
    }

    [GraphQLSort]
    public IQueryable<Edition> Editions { get; set; }

    [MustBeSalesman]
    [GraphQLDescription("Returns the exact inventory")]
    [GraphQLDeprecated("Will be 0 from 2024-01-01")]
    public int ExactInventory { get; set; }
}