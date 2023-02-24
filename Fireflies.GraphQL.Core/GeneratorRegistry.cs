using Fireflies.GraphQL.Core.Generators;
using Fireflies.GraphQL.Core.Generators.Connection;
using Fireflies.GraphQL.Core.Generators.Sorting;

namespace Fireflies.GraphQL.Core;

internal static class GeneratorRegistry {
    private static List<IGenerator> Registered = new();

     public static IEnumerable<T> GetGenerators<T>() where T : IGenerator {
        return Registered.OfType<T>();
    }

    static GeneratorRegistry() {
        Registered.Add(new SortingExtenderGenerator());
        Registered.Add(new ConnectionGenerator());
    }
}