using Fireflies.GraphQL.Abstractions;
using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Abstractions.Sorting;

namespace Fireflies.GraphQL.Demo;

public class BookOperations {
    [GraphQlPagination]
    [GraphQLQuery]
    [GraphQLSort]
    public Task<IEnumerable<InventoryBook>> Books(BookFilterInput? filter) {
        return Task.FromResult(new InventoryBook[] {
            new() { BookId = 1, Title = "My first book", ISBN = "1234", ExactInventory = 20, Editions = new[] { new Edition { Name = "Deluxeutgåva", Released = DateTimeOffset.UtcNow.AddYears(-1) }, new Edition { Name = "First", Released = DateTimeOffset.UtcNow.AddYears(-2) } } },
            new() { BookId = 2, Title = "My second book", ISBN = "5678", ExactInventory = 29 },
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