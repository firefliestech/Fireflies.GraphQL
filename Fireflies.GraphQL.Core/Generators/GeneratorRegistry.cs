using Fireflies.GraphQL.Core.Exceptions;

namespace Fireflies.GraphQL.Core.Generators;

internal class GeneratorRegistry {
    private readonly List<object> _registered = new();

    public IEnumerable<T> GetGenerators<T>() where T : IGenerator {
        return _registered.OfType<T>();
    }

    public void Add<TGenerator>(TGenerator generator) where TGenerator : IGenerator {
        _registered.Add(generator);
    }

    public void AddBefore<TAddBefore>(IGenerator generator) where TAddBefore : IGenerator {
        var oldIndex = _registered.FindIndex(x => x is TAddBefore);
        if(oldIndex == -1)
            throw new GraphQLException($"Cant find a generator with type {typeof(TAddBefore).Name}");

        _registered.Insert(oldIndex, generator);
    }
}