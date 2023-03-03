using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

public interface IExtensionBuilder {
    void Build();
    void BuildRequestLifetimeScope(ILifetimeScopeBuilder lifetimeScopeBuilder);
    void BuildGraphQLLifetimeScope(ILifetimeScopeBuilder lifetimeScopeBuilder);
}