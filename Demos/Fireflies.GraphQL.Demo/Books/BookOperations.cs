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
    [GraphQLParallel]
    public IEnumerable<IBook> Books() {
        return YieldBooks();
    }

    [GraphQLQuery]
    public async Task<IBook> Book(int bookId) {
        return YieldBooks().First();
    }

    [GraphQLMutation]
    public Task<InventoryBook> AddBook(AddBookInput data) {
        return Task.FromResult(new InventoryBook {
            BookId = DateTime.UtcNow.Second,
            Title = data.Title ?? "<EMPTY"
        });
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<IBook> BookUpdated(int bookId, ASTNode astNode, [EnumeratorCancellation] CancellationToken cancellation) {
        while(!cancellation.IsCancellationRequested) {
            await Task.Delay(7500, cancellation).ConfigureAwait(false);
            yield return CreateInventoryBook(bookId);
        }
    }

    ///////////////////////////////////////
    // Helper methods

    private IEnumerable<IBook> YieldBooks() {
        for(var i = 0; i < 100; i++) {
            yield return CreateInventoryBook(i);
        }

        for(var i = 0; i < 100; i++) {
            yield return CreateRemoteBook(i);
        }
    }

    private static RemoteBook CreateRemoteBook(int i) {
        return new RemoteBook {
            BookId = i,
            Title = $"My {i}th book",
            ISBN = "1234",
        };
    }

    private static InventoryBook CreateInventoryBook(int i) {
        return new InventoryBook {
            BookId = i, Title = $"My {i}th book", ISBN = "1234", ExactInventory = 20,
            Editions = new[] {
                new Edition { Name = "Deluxe", Released = DateTimeOffset.UtcNow.AddYears(-1) },
                new Edition { Name = "Original", Released = DateTimeOffset.UtcNow.AddYears(-2) }
            }.AsQueryable()
        };
    }
}