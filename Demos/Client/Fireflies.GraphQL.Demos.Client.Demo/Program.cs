// See https://aka.ms/new-console-template for more information
//using MyNamespace;

using Fireflies.GraphQL.Demos.GraphQL.GraphQLDemo;

Console.WriteLine("Here we go...");

var client = new GraphQLDemoClient(h => {
    h.Uri = new Uri("https://localhost:7273/graphql");
}, h => {
    h.Uri = new Uri("wss://localhost:7273/graphql");
    h.ReconnectDelay = TimeSpan.FromSeconds(5);
});

client.Connected += () => Console.WriteLine("Connected via client");
client.Connecting += () => Console.WriteLine("Connecting");
client.Reconnecting += () => Console.WriteLine("Reconnecting");
client.Disconnected += () => Console.WriteLine("Disconnected");

Console.WriteLine("The first 10 books are");
var allBooks = await client.GetBooks().ConfigureAwait(false);
foreach(var book in allBooks.Data.Books.Take(10)) {
    Console.WriteLine($" - {book.Title}");
}

Console.WriteLine("Adding a new book");
var addedBook = await client.AddBook("My new book");
Console.WriteLine($"- Added book got ID: {addedBook.Data.AddBook.BookId}");


Console.WriteLine("Subscribing to changes");
var watcher = await client.BookUpdated(23).Watch(m => {
    Console.WriteLine($"- Update received: {m.Data.BookUpdated.Title} {m.Data.BookUpdated.BookId}");
});

Console.WriteLine("Will exit on ENTER");
Console.ReadLine();

await watcher.DisposeAsync();
