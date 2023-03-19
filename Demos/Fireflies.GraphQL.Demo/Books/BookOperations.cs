using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Connection;
using Fireflies.GraphQL.Abstractions.Sorting;
using Fireflies.GraphQL.Abstractions.Where;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Demo.Books;

public class BookOperations {
    [GraphQLPagination]
    [GraphQLQuery]
    [GraphQLSort]
    [GraphQLWhere]
    public IEnumerable<InventoryBook> Books(BookFilterInput? filter) {
        var x = YieldBooks()
            .Where(x => x.BookId != 1 || filter == null || string.IsNullOrWhiteSpace(filter.Title) || x.Title == filter.Title);
        return x;
    }

    [GraphQLQuery]
    public async Task<InventoryBook> GetBook() {
        return null;
    }

    private IEnumerable<InventoryBook> YieldBooks() {
        Console.WriteLine("Yielding 1");
        yield return new() { BookId = 1, Title = "My first book", ISBN = "1234", ExactInventory = 20, Editions = new[] { new Edition { Name = "Deluxeutgåva", Released = DateTimeOffset.UtcNow.AddYears(-1) }, new Edition { Name = "First", Released = DateTimeOffset.UtcNow.AddYears(-2) } }.AsQueryable() };

        Console.WriteLine("Yielding 2");
        yield return new() { BookId = 2, Title = "My second book", ISBN = "5678", ExactInventory = 29 };
    }

    [GraphQLMutation]
    public Task<InventoryBook> AddBook(AddBookInput data) {
        return Task.FromResult(new InventoryBook {
            BookId = DateTime.UtcNow.Second,
            Title = data.Title ?? "<EMPTY"
        });
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<InventoryBook> BookUpdated(int bookId, ASTNode astNode, [EnumeratorCancellation] CancellationToken cancellation) {
        while(!cancellation.IsCancellationRequested) {
            await Task.Delay(2000, cancellation).ConfigureAwait(false);
            yield return new InventoryBook { BookId = 1, Title = "My first book was updated", ISBN = "1234", ExactInventory = 20 };
        }
    }
}