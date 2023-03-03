using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

public class ExtensionRegistry {
    private readonly List<IExtensionBuilder> _extensionBuilders = new();

    public void Add(IExtensionBuilder builder) {
        _extensionBuilders.Add(builder);
    }

    public void BuildOptions() {
        foreach(var extensionBuilder in _extensionBuilders) {
            extensionBuilder.Build();
        }
    }

    public void BuildRequestLifetimeScope(ILifetimeScopeBuilder lifetimeScopeBuilder) {
        foreach(var extensionBuilder in _extensionBuilders) {
            extensionBuilder.BuildRequestLifetimeScope(lifetimeScopeBuilder);
        }
    }

    public void BuildGraphQLLifetimeScope(ILifetimeScopeBuilder lifetimeScopeBuilder) {
        foreach(var extensionBuilder in _extensionBuilders) {
            extensionBuilder.BuildGraphQLLifetimeScope(lifetimeScopeBuilder);
        }
    }
}