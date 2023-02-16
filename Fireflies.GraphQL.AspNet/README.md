# Fireflies GraphQL ASP.NET middleware

## Example
Add the following code to your WebApplication pipeline.
```
app.UseWebSockets();

var graphQLOptions = new GraphQLOptionsBuilder();
graphQLOptions.Add<BookOperations>();
app.UseGraphQL(await graphQLOptions.Build());
```

## Operation definition
```
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

public class BookFilterInput : GraphQLInput {
    public string? Title { get; set; }
    public StringFilterOperatorInput? ISBN { get; set; }
}


public class AddBookInput : GraphQLInput {
    public string Title { get; set; }
}

```

#### Return types
```
[GraphQLUnion]
public interface IBook {
    public int BookId { get; set; }

    string Title { get; set; }
    string ISBN { get; set; }
}

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
```

#### Input definitions
```
public class BookFilterInput : GraphQLInput {
    public string? Title { get; set; }
    public StringFilterOperatorInput? ISBN { get; set; }
}

public class AddBookInput : GraphQLInput {
    public string Title { get; set; }
}

public class StringFilterOperatorInput : GraphQLInput {
    public string? Eq { get; set; }
}
```

#### Authorization
Please note that the default constructor is marked as internal. This will make the IoC system to select the version which has a User as an argument.

The User object can be registered using a implementation of the IRequestDependencyResolverBuilder interface.

```
public class MustBeSalesmanAttribute : GraphQLAuthorizationAttribute {
    internal MustBeSalesmanAttribute() {
    }

    public MustBeSalesmanAttribute(User user) {
    }

    public override Task<bool> Authorize() {
        return Task.FromResult(false);
    }

    public override string Help => "Must be authenticated as a salesman";
}
```

#### Request container
To setup the container per request you need to define a class that implements the IRequestDependencyResolverBuilder interface.

```
public class RequestDependencyResolverBuilder : IRequestDependencyResolverBuilder {
    public void Build(ILifetimeScopeBuilder builder, HttpContext context) {
        builder.RegisterInstance(new User());
    }
}
```

This resolver needs to be registered within your root container, example below is using Fireflies.IoC.Autofac.

```
var containerBuilder = new ContainerBuilder();
containerBuilder.RegisterType<MustBeSalesmanAttribute>();
containerBuilder.RegisterType<RequestDependencyResolverBuilder>().As<IRequestDependencyResolverBuilder>();
var container = containerBuilder.Build();

...

graphQLOptions.SetDependencyResolver(new AutofacDependencyResolver(container));
```

## Federation
To add a federated backend, you only need to register it during startup.

```
graphQLOptions.AddFederation("Author", "https://localhost:7274/graphql");
```
