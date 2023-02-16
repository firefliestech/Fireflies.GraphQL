using Fireflies.GraphQL.AspNet;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.FederationDemo; 

public class RequestDependencyResolverBuilder : IRequestDependencyResolverBuilder {
    public void Build(ILifetimeScopeBuilder builder, HttpContext context) {
        builder.RegisterInstance(new User());
    }
}