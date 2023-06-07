using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

public interface IRequestContainerBuilder<in THttpContext> {
    void Build(ILifetimeScopeBuilder builder, THttpContext httpContext);
}