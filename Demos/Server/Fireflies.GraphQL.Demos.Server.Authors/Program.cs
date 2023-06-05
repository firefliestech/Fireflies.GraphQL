using Autofac;
using Fireflies.GraphQL.AspNet;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Demos.Server.Authors;
using Fireflies.GraphQL.Demos.Server.Authors.Authors;
using Fireflies.IoC.Autofac;
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

var containerBuilder = new ContainerBuilder();
containerBuilder.RegisterType<RequestDependencyResolverBuilder>().As<IRequestDependencyResolverBuilder>();
var container = containerBuilder.Build();

var graphQLOptions = new GraphQLOptionsBuilder();
graphQLOptions.SetLoggerFactory(new FirefliesNLogFactory());
graphQLOptions.Add<AuthorOperations>();
graphQLOptions.SetDependencyResolver(new AutofacDependencyResolver(container));
app.UseWebSockets();
app.UseGraphQL(await graphQLOptions.Build());

app.MapGet("/", () => "Hello World!");

app.Run();