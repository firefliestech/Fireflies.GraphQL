using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Generators.Sorting;

namespace Fireflies.GraphQL.Extensions.EntityFramework;

public static class EntityFrameworkGraphQLOptionsBuilder {
    public static void UseEntityFramework(this GraphQLOptionsBuilder optionsBuilder) {
        optionsBuilder.AddGeneratorBefore<SortingGenerator>(new EntityFrameworkMethodExtender());
    }
}