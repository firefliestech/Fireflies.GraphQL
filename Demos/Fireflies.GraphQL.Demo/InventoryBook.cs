using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Demo;

public class InventoryBook : IBook {
    [GraphQlId]
    public int BookId { get; set; }

    public string Title { get; set; }
    public string ISBN { get; set; }

    public string Echo(int i) {
        return "Du angav " + i;
    }

    public async Task<string> Echo2(int i)
    {
        return "Du angav ett mindre än " + (i + 1);
    }
    
    [MustBeSysopToSee]
    [GraphQLDescription("Kalle anka")]
    [GraphQLDeprecated("Du borde veta")]
    public int ExactInventory { get; set; }
}