using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Federation;
using Fireflies.GraphQL.Core.Schema;
using Fireflies.IoC.Abstractions;
using Fireflies.IoC.TinyIoC;
using Fireflies.Logging.Abstractions;

namespace Fireflies.GraphQL.Core;

public class GraphQLOptionsBuilder {
    private readonly HashSet<Type> _operationTypes = new();
    private IDependencyResolver _dependencyResolver = new TinyIoCDependencyResolver();
    private string _url = "/graphql";
    private IFirefliesLoggerFactory _loggerFactory = new NullLoggerFactory();
    private readonly HashSet<(string Name, string Url)> _federations = new();
    private string? _schemaDescription;

    public GraphQLOptionsBuilder() {
        _operationTypes.Add(typeof(__SchemaQuery));
    }

    public GraphQLOptionsBuilder Add<T>() {
        _operationTypes.Add(typeof(T));
        return this;
    }

    public GraphQLOptionsBuilder SetDependencyResolver(IDependencyResolver dependencyResolver) {
        _dependencyResolver = dependencyResolver;
        return this;
    }

    public GraphQLOptionsBuilder SetSchemaDescription(string schemaDescription) {
        _schemaDescription = schemaDescription;
        return this;
    }

    public GraphQLOptionsBuilder SetUrl(string url) {
        _url = url;
        return this;
    }

    public async Task<GraphQLOptions> Build() {
        var logger = _loggerFactory.GetLogger<GraphQLOptionsBuilder>();

        logger.Trace("Building GraphQLOptions. ");

        var options = new GraphQLOptions {
            Url = _url,
            SchemaDescription = _schemaDescription
        };

        foreach(var federation in _federations) {
            var attempt = 0;
            while(true) {
                attempt++;
                try {
                    logger.Info($"Fetching federated schema for {federation.Name} from {federation.Url}");
                    var federationSchema = await new FederationClient(federation.Url).FetchSchema();
                    var generator = new FederationGenerator(federation, federationSchema);
                    var generatedType = generator.Generate();
                    _operationTypes.Add(generatedType);
                    break;
                } catch(Exception ex) {
                    
                    if(attempt < 4) {
                        const int delay = 3000;
                        logger.Error(ex, $"Failed to add federation. Attempt: {attempt}. Retrying in {delay}ms");
                        await Task.Delay(delay);
                    } else {
                        logger.Error(ex, $"Failed to add federation. Attempt: {attempt}. Giving up");
                        throw;
                    }
                }
            }
        }

        options.DependencyResolver = _dependencyResolver.BeginLifetimeScope(builder => {
            builder.RegisterInstance(options);
            builder.RegisterType<GraphQLEngine>();
            builder.RegisterType<__SchemaQuery>();

            foreach(var type in _operationTypes) {
                foreach(var operation in FindOperations(type, wt => wt.GetAllGraphQLQueryMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.QueryOperations.Add(operation);
                    logger.Debug($"Added query operation {operation.Name} from {operation.Type.FullName}");
                }

                foreach(var operation in FindOperations(type, wt => wt.GetAllGraphQLMutationMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.MutationsOperations.Add(operation);
                    logger.Debug($"Added mutation operation {operation.Name} from {operation.Type.FullName}");
                }

                foreach(var operation in FindOperations(type, wt => wt.GetAllGraphQLSubscriptionMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.SubscriptionOperations.Add(operation);
                    logger.Debug($"Added subscription operation {operation.Name} from {operation.Type.FullName}");
                }
            }

            var schemaBuilder = new SchemaBuilder(options);
            var schema = schemaBuilder.GenerateSchema();
            builder.RegisterInstance(schema);
        });

        var validator = new SchemaValidator(options.AllOperations);
        validator.Validate();

        options.LoggerFactory = _loggerFactory;

        logger.Trace("Options built successfully");

        return options;
    }

    private IEnumerable<OperationDescriptor> FindOperations(Type type, Func<Type, IEnumerable<MethodInfo>> getMethodsCallback) {
        var wrappedType = WrapperGenerator.GenerateWrapper(type);

        foreach(var method in getMethodsCallback(wrappedType)) {
            var operation = new OperationDescriptor(method.GraphQLName(),
                wrappedType,
                method);

            yield return operation;
        }
    }

    public void SetLoggerFactory(IFirefliesLoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory;
    }

    public void AddFederation(string name, string url) {
        _federations.Add((name, url));
    }
}