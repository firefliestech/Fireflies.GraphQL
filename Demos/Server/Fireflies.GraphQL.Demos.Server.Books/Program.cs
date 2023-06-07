using Fireflies.GraphQL.AspNet;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Demos.Server.Books;
using Fireflies.GraphQL.Demos.Server.Books.Blogs;
using Fireflies.GraphQL.Demos.Server.Books.Books;
using Fireflies.GraphQL.Extensions.EntityFrameworkCore;
using Fireflies.IoC.TinyIoC;
using Fireflies.Logging.NLog;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(builder =>
        builder.SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();
    
app.UseCors(corsPolicyBuilder => corsPolicyBuilder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

var config = new LoggingConfiguration();
InternalLogger.LogLevel = LogLevel.Error;
InternalLogger.LogToConsole = true;
var consoleTarget = new ColoredConsoleTarget();
config.AddTarget("console", consoleTarget);
config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));
LogManager.Configuration = config;

var container = new TinyIoCContainer();
container.Register<MustBeSalesmanAttribute>();
container.Register<IRequestContainerBuilder, RequestContainerBuilder>();

// Enable websockets (needed for subscriptions)
app.UseWebSockets();

///////////////////////////////////////
// Start GraphQL setup
var graphQLOptions = new GraphQLOptionsBuilder();

// Add entity framework support
var entityFrameworkOptions = graphQLOptions.UseEntityFramework();
entityFrameworkOptions.Register<BloggingContext>();

// Set framework libraries
graphQLOptions.SetDependencyResolver(new TinyIoCDependencyResolver(container));
graphQLOptions.SetLoggerFactory(new FirefliesNLogFactory());

//// Add operations
graphQLOptions.Add<BookOperations>();
graphQLOptions.Add<BlogOperations>();

// Add federation
//graphQLOptions.AddFederation("Author", "https://localhost:7274/graphql");

// Add to pipeline
app.UseGraphQL(await graphQLOptions.Build());

// End GraphQL setup
///////////////////////////////////////

app.MapGet("/", () => "Hello World!");

app.Run();