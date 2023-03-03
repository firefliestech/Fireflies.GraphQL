using Fireflies.GraphQL.Core;

namespace Fireflies.GraphQL.Extensions.EntityFramework;

public static class EntityFrameworkGraphQLOptionsBuilder {
    public static EntityFrameworkExtensionsBuilder UseEntityFramework(this GraphQLOptionsBuilder optionsBuilder) {
        var builder = new EntityFrameworkExtensionsBuilder(optionsBuilder);
        optionsBuilder.AddExtension(builder);
        return builder;
    }
}