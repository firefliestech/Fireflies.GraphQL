using Fireflies.GraphQL.Core.Generators;

namespace Fireflies.GraphQL.Core;

internal class GeneratorRegistry {
    private readonly List<object> _registered = new();

    public IEnumerable<T> GetGenerators<T>() where T : IGenerator {
        return _registered.OfType<T>();
    }

    public void Add<TGenerator>(TGenerator generator) where TGenerator : IGenerator {
        _registered.Add(generator);
    }
}