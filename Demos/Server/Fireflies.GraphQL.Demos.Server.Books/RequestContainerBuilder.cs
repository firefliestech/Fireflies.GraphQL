using Fireflies.GraphQL.Core;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Demos.Server.Books; 

public class RequestContainerBuilder : IRequestContainerBuilder<HttpContext> {
    public void Build(ILifetimeScopeBuilder builder, HttpContext httpContext) {
        builder.RegisterInstance(new User());
    }
}