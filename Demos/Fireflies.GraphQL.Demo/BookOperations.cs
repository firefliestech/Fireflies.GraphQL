using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core;

namespace Fireflies.GraphQL.Demo;

public class BookOperations {
    private readonly IGraphQLContext _context;

    public BookOperations(IGraphQLContext context) {
        _context = context;
    }

    [GraphQlPagination]
    [GraphQLQuery]
    public async Task<IEnumerable<InventoryBook>> Books(BookFilterInput? filter) {
        return new InventoryBook[] {
            new() { BookId = 1, Title = "My first book", ISBN = "1234", ExactInventory = 20 },
            new() { BookId = 2, Title = "My second book", ISBN = "5678", ExactInventory = 29 }
        }.Where(x => filter == null || string.IsNullOrWhiteSpace(filter.Title) || x.Title == filter.Title);
    }

    [GraphQLMutation]
    public async Task<InventoryBook> AddBook(AddBookInput data) {
        return new InventoryBook {
            BookId = DateTime.UtcNow.Second,
            Title = data.Title
        };
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<InventoryBook> BookUpdated(int bookId) {
        while(!_context.CancellationToken.IsCancellationRequested) {
            await Task.Delay(2000);
            yield return new() { BookId = 1, Title = "My first book was updated", ISBN = "1234", ExactInventory = 20 };
        }
    }
}