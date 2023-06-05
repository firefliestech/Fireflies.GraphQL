using Fireflies.GraphQL.AspNet;
using Fireflies.GraphQL.Core;
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

// Enable websockets (needed for subscriptions)
app.UseWebSockets();

///////////////////////////////////////
// Start GraphQL setup
var graphQLOptions = new GraphQLOptionsBuilder();

// Set framework libraries
graphQLOptions.SetDependencyResolver(new TinyIoCDependencyResolver(container));
graphQLOptions.SetLoggerFactory(new FirefliesNLogFactory());

// Add federation
graphQLOptions.AddFederation("Books", "https://localhost:7273/graphql");
graphQLOptions.AddFederation("Authors", "https://localhost:7274/graphql");

// Add to pipeline
app.UseGraphQL(await graphQLOptions.Build());

// End GraphQL setup
///////////////////////////////////////

app.MapGet("/", () => "Hello World!");

app.Run();