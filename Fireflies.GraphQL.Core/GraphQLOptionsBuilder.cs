using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Federation;
using Fireflies.GraphQL.Core.Generators;
using Fireflies.GraphQL.Core.Generators.Connection;
using Fireflies.GraphQL.Core.Generators.Sorting;
using Fireflies.GraphQL.Core.Generators.Where;
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
    private readonly GeneratorRegistry _generatorRegistry;

    public static int _optionCounter = 0;
    private readonly ModuleBuilder _moduleBuilder;
    private readonly List<IExtensionBuilder> _extensions = new();

    public GraphQLOptionsBuilder() {
        _operationTypes.Add(typeof(__SchemaQuery));

        var assemblyName = new AssemblyName($"Fireflies.GraphQL.ProxyAssembly{_optionCounter++}");
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = dynamicAssembly.DefineDynamicModule("Main");

        _generatorRegistry = new GeneratorRegistry();
        _generatorRegistry.Add(new WhereGenerator(_moduleBuilder));
        _generatorRegistry.Add(new SortingGenerator(_moduleBuilder));
        _generatorRegistry.Add(new ConnectionGenerator(_moduleBuilder));
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

    public void AddGenerator(IGenerator generator) {
        _generatorRegistry.Add(generator);
    }

    public void AddGeneratorBefore<TAddBefore>(IGenerator generator) where TAddBefore : IGenerator {
        _generatorRegistry.AddBefore<TAddBefore>(generator);
    }

    public async Task<GraphQLOptions> Build() {
        var logger = _loggerFactory.GetLogger<GraphQLOptionsBuilder>();

        logger.Trace("Building GraphQLOptions. ");

        var options = new GraphQLOptions {
            Url = _url,
            SchemaDescription = _schemaDescription
        };

        foreach(var extension in _extensions)
            options.Extensions.Add(extension);

        options.Extensions.BuildOptions();

        foreach(var federation in _federations) {
            var attempt = 0;
            while(true) {
                attempt++;
                try {
                    logger.Info($"Fetching federated schema for {federation.Name} from {federation.Url}");
                    var federationSchema = await new FederationClient(federation.Url).FetchSchema().ConfigureAwait(false);
                    var generator = new FederationGenerator(federation, federationSchema);
                    var generatedType = generator.Generate();
                    _operationTypes.Add(generatedType);
                    break;
                } catch(Exception ex) {
                    if(attempt < 4) {
                        const int delay = 3000;
                        logger.Error(ex, $"Failed to add federation. Attempt: {attempt}. Retrying in {delay}ms");
                        await Task.Delay(delay).ConfigureAwait(false);
                    } else {
                        logger.Error(ex, $"Failed to add federation. Attempt: {attempt}. Giving up");
                        throw;
                    }
                }
            }
        }

        var wrapperRegistry = new WrapperRegistry();
        var wrapperGenerator = new WrapperGenerator(_moduleBuilder, _generatorRegistry, wrapperRegistry);

        options.DependencyResolver = _dependencyResolver.BeginLifetimeScope(builder => {
            builder.RegisterInstance(_moduleBuilder);
            builder.RegisterInstance(options);
            builder.RegisterType<GraphQLEngine>();
            builder.RegisterType<__SchemaQuery>();
            builder.RegisterInstance(wrapperRegistry);

            options.Extensions.BuildGraphQLLifetimeScope(builder);

            foreach(var type in _operationTypes) {
                foreach(var operation in FindOperations(wrapperGenerator, type, wt => wt.GetAllGraphQLQueryMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.QueryOperations.Add(operation);
                    logger.Debug($"Added query operation {operation.Name} from {operation.Type.FullName}");
                }

                foreach(var operation in FindOperations(wrapperGenerator, type, wt => wt.GetAllGraphQLMutationMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.MutationsOperations.Add(operation);
                    logger.Debug($"Added mutation operation {operation.Name} from {operation.Type.FullName}");
                }

                foreach(var operation in FindOperations(wrapperGenerator, type, wt => wt.GetAllGraphQLSubscriptionMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.SubscriptionOperations.Add(operation);
                    logger.Debug($"Added subscription operation {operation.Name} from {operation.Type.FullName}");
                }
            }

            var schemaBuilder = new SchemaBuilder(options, wrapperRegistry);
            var schema = schemaBuilder.GenerateSchema();
            builder.RegisterInstance(schema);
        });

        var validator = new SchemaValidator(options.AllOperations);
        validator.Validate();

        options.LoggerFactory = _loggerFactory;

        logger.Trace("Options built successfully");

        return options;
    }

    private IEnumerable<OperationDescriptor> FindOperations(WrapperGenerator wrapperGenerator, Type type, Func<Type, IEnumerable<MethodInfo>> getMethodsCallback) {
        var wrappedType = wrapperGenerator.GenerateWrapper(type);

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

    public void AddExtension(IExtensionBuilder builder) {
        _extensions.Add(builder);
    }
}