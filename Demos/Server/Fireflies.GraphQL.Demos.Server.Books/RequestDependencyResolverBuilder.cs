using Fireflies.GraphQL.Core;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Demos.Server.Books; 

public class RequestDependencyResolverBuilder : IRequestDependencyResolverBuilder {
    public void Build(ILifetimeScopeBuilder builder, IConnectionContext context) {
        builder.RegisterInstance(new User());
    }
}