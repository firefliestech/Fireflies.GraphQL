using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

public class RequestContainerFactory {
    private readonly GraphQLOptions _options;

    public RequestContainerFactory(GraphQLOptions options) {
        _options = options;
    }

    public IDependencyResolver Create(IConnectionContext connectionContext) {
        var lifetimeScopeResolver = _options.DependencyResolver.BeginLifetimeScope(builder => {
            builder.RegisterType<GraphQLEngine>();
            builder.RegisterInstance(_options.LoggerFactory);
            builder.RegisterInstance(connectionContext);
            if(_options.DependencyResolver.TryResolve<IRequestContainerBuilder>(out var innerBuilder)) {
                innerBuilder!.Build(builder, connectionContext);
            }

            _options.Extensions.BuildRequestLifetimeScope(builder);
        });

        return lifetimeScopeResolver;
    }
}