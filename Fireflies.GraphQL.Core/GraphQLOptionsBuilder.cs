using System.Reflection;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Schema;
using Fireflies.IoC.Core;
using Fireflies.IoC.TinyIoC;
using Fireflies.Logging.Core;

namespace Fireflies.GraphQL.Core;

public class GraphQLOptionsBuilder {
    private readonly HashSet<Type> _operationTypes = new();
    private IDependencyResolver _dependencyResolver = new TinyIoCDependencyResolver();
    private string _url = "/graphql";
    private IFirefliesLoggerFactory _loggerFactory = new NullLoggerFactory();

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

    public GraphQLOptionsBuilder SetUrl(string url) {
        _url = url;
        return this;
    }

    public GraphQLOptions Build() {
        var options = new GraphQLOptions {
            Url = _url
        };

        options.DependencyResolver = _dependencyResolver.BeginLifetimeScope(builder => {
            builder.RegisterInstance(options);
            builder.RegisterType<GraphQLEngine>();
            builder.RegisterType<__SchemaQuery>();

            foreach(var type in _operationTypes) {
                foreach(var operation in FindOperations(type, wt => wt.GetAllGraphQLQueryMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.QueryOperations.Add(operation);
                }

                foreach(var operation in FindOperations(type, wt => wt.GetAllGraphQLMutationMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.MutationsOperations.Add(operation);
                }

                foreach(var operation in FindOperations(type, wt => wt.GetAllGraphQLSubscriptionMethods(true))) {
                    builder.RegisterType(operation.Type);
                    options.SubscriptionOperations.Add(operation);
                }
            }
        });

        options.LoggerFactory = _loggerFactory;

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
}