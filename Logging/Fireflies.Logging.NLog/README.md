# Fireflies NLog integration

## Add logger factory
```
var graphQLOptions = new GraphQLOptionsBuilder();
graphQLOptions.SetLoggerFactory(new FirefliesNLogFactory());
```
