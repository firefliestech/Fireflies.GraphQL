using Fireflies.GraphQL.Core;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Demos.Server.Authors; 

public class RequestContainerBuilder : IRequestContainerBuilder {
    public void Build(ILifetimeScopeBuilder builder, IConnectionContext context) {
        builder.RegisterInstance(new User());
    }
}