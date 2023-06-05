using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

public interface IRequestContainerBuilder {
    void Build(ILifetimeScopeBuilder builder, IConnectionContext context);
}