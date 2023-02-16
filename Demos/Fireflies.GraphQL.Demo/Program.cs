using Autofac;
using Fireflies.GraphQL.AspNet;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Demo;
using Fireflies.IoC.Autofac;
using Fireflies.Logging.NLog;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

var builder = WebApplication.CreateBuilder(args);

//TODO: Make sure pagination nodes has id attribute

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

var containerBuilder = new ContainerBuilder();
containerBuilder.RegisterType<MustBeSysopToSeeAttribute>();
containerBuilder.RegisterType<RequestDependencyResolverBuilder>().As<IRequestDependencyResolverBuilder>();
var container = containerBuilder.Build();

await Task.Delay(1000);
var graphQLOptions = new GraphQLOptionsBuilder();
graphQLOptions.SetLoggerFactory(new FirefliesNLogFactory());
graphQLOptions.Add<BookOperations>();
graphQLOptions.AddFederation("Author", "https://localhost:7274/graphql");
graphQLOptions.SetDependencyResolver(new AutofacDependencyResolver(container));
app.UseWebSockets();
app.UseGraphQL(await graphQLOptions.Build());

app.MapGet("/", () => "Hello World!");

app.Run();