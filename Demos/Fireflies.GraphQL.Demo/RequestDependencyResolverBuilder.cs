using Fireflies.GraphQL.AspNet;
using Fireflies.GraphQL.Demo;
using Fireflies.IoC.Core;

public class RequestDependencyResolverBuilder : IRequestDependencyResolverBuilder {
    public void Build(ILifetimeScopeBuilder builder, HttpContext context) {
        builder.RegisterInstance(new User());
    }
}