using Fireflies.GraphQL.Contract;
using Fireflies.GraphQL.Core;

namespace Fireflies.GraphQL.Demo;

public class BookOperations {
    private readonly GraphQLContext _context;
    private readonly InventoryBook _inventoryBook;

    public BookOperations(GraphQLContext context) {
        _context = context;
        _inventoryBook = new InventoryBook {
            BookId = 1,
            Title = "Min första bok",
            ISBN = "01234",
            ExactInventory = 15
        };
    }

    [GraphQLQuery]
    public IEnumerable<InventoryBook> SyncBooks() {
        Thread.Sleep(1000);
        return new InventoryBook[] {
            _inventoryBook,
            new InventoryBook() {
                BookId = 2,
                Title = "Min andra bok",
                ISBN = "56789",
                ExactInventory = 29
            }
        };
    }

    [GraphQlPagination]
    [GraphQLQuery]
    public async Task<IEnumerable<InventoryBook>> Books(BookFilterInput? filter) {
        //  = new BookFilterInput { Title = "Min första bok" }
        return new InventoryBook[] {
            _inventoryBook,
            new InventoryBook() {
                BookId = 2,
                Title = "Min andra bok",
                ISBN = "56789",
                ExactInventory = 29
            }
        }.Where(x => filter == null || string.IsNullOrWhiteSpace(filter.Title) || x.Title == filter.Title);
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<InventoryBook> BookUpdated(int bookId) {
        while(!_context.CancellationToken.IsCancellationRequested) {
            await Task.Delay(2000);
            _inventoryBook.Title = "FIXADE TITELN!";
            yield return _inventoryBook;
        }

        Console.WriteLine("EX");
    }

    [GraphQLMutation]
    public async Task<InventoryBook> AddBook(AddBookInput data) {
        return new InventoryBook {
            BookId = DateTime.UtcNow.Second,
            Title = data.Title
        };
    }

    public async Task<InventoryBook> Book() {
        return _inventoryBook;
    }
}