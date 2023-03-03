using Fireflies.GraphQL.Core;

namespace Fireflies.GraphQL.Extensions.EntityFrameworkCore;

public static class EntityFrameworkCoreGraphQLOptionsBuilder {
    public static EntityFrameworkCoreExtensionsBuilder UseEntityFramework(this GraphQLOptionsBuilder optionsBuilder) {
        var builder = new EntityFrameworkCoreExtensionsBuilder(optionsBuilder);
        optionsBuilder.AddExtension(builder);
        return builder;
    }
}