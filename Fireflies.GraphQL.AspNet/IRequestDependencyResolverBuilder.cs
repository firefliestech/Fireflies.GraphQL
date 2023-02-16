using Fireflies.IoC.Core;
using Microsoft.AspNetCore.Http;

namespace Fireflies.GraphQL.AspNet;

public interface IRequestDependencyResolverBuilder {
    void Build(ILifetimeScopeBuilder builder, HttpContext context);
}