using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core;
using System.Runtime.CompilerServices;

namespace Fireflies.GraphQL.Demo;

public class BookOperations {
    [GraphQlPagination]
    [GraphQLQuery]
    public Task<IEnumerable<InventoryBook>> Books(BookFilterInput? filter) {
        return Task.FromResult(new InventoryBook[] {
            new() { BookId = 1, Title = "My first book", ISBN = "1234", ExactInventory = 20 },
            new() { BookId = 2, Title = "My second book", ISBN = "5678", ExactInventory = 29 }
        }.Where(x => filter == null || string.IsNullOrWhiteSpace(filter.Title) || x.Title == filter.Title));
    }

    [GraphQLMutation]
    public Task<InventoryBook> AddBook(AddBookInput data) {
        return Task.FromResult(new InventoryBook {
            BookId = DateTime.UtcNow.Second,
            Title = data.Title ?? "<EMPTY"
        });
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<InventoryBook> BookUpdated(int bookId, [EnumeratorCancellation] CancellationToken cancellation) {
        while(!cancellation.IsCancellationRequested) {
            await Task.Delay(2000, cancellation);
            yield return new InventoryBook { BookId = 1, Title = "My first book was updated", ISBN = "1234", ExactInventory = 20 };
        }
    }
}