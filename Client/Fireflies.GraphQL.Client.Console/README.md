# Fireflies GraphQL client generator

The generated generates files from a schema and does not add external dependencies to your project.

## Install the tool

### Globally
```
dotnet tool install --global Fireflies.GraphQL.Client.Console
```

### Locally
```
dotnet tool install Fireflies.GraphQL.Client.Console
```

## Init a project
Basically creates a GraphQL in your project and add general generation settings to Settings.json.
```
fireflies-graphql project-init --path path-to-root-of-project --namespace MyProject.Api.GraphQL --service-collection
```

## Init a client
Downloads the schema and stores it into a sub-folder inside the GraphQL.
```
fireflies-graphql client-init --name MyProject --path path-to-project --uri https://localhost:7273/graphql
```

## Update the schema
```
fireflies-graphql client-update --name MyProject --path path-to-project
```

## Add your GraphQL queries
Under the GraphQL\MyProject folder you can now add .graphql files that will be used to generate the client.
```
query GetBook($bookId: Int!) {
	getBook(bookId: $bookId) {
		...TitleFragment
	}
}

fragment TitleFragment on IBook {
	title
	... on InventoryBook {
		calculatePrice
		editions {
			name
			released
		}
	}
	... TestFragment
}

fragment TestFragment on RemoteBook {
	test
}

subscription BookUpdated($bookId: Int!) {
  bookUpdated(bookId: $bookId) {
    title
	bookId
    __typename
  }
}
```

## Generate all clients
```
fireflies-graphql generate --path path-to-project
```

## Using the client

### Setup the client
```
var client = new MyProjectClient(h => {
    h.SetUri(new Uri("https://localhost:7273/graphql"));
}, h => {
    h.SetUri(new Uri("wss://localhost:7273/graphql"));
});
```

### Performing a query
```
var bookResult = await client.GetBook(123);
var book = bookResult.Data;
```

### Subscribe to a subscription
```
var watcher = await client.BookUpdated(123).Watch(m => {
    Console.WriteLine($"{m.Data.BookUpdated.BookId} was updated {m.Data.BookUpdated.Title}");
});
```

Once you´re done with your subscription make sure to dispose it.
```
await watcher.DisposeAsync();
```

_Logo by freepik_