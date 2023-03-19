namespace Fireflies.GraphQL.Abstractions;

public abstract class GraphQLId {
}

public class GraphQLId<T> : GraphQLId {
    private readonly T _value;

    public GraphQLId(T value) {
        _value = value;
    }

    public override string ToString() {
        return _value?.ToString()!;
    }

    public static implicit operator T(GraphQLId<T> value) => value._value;
    public static implicit operator GraphQLId<T>(T value) => new(value);
}