namespace Fireflies.GraphQL.Core;

public class WrapperRegistry {
    private readonly Dictionary<Type, Type> _wrappedTypes = new();

    public bool TryGetValue(Type type, out Type existingWrapper) {
        if(_wrappedTypes.TryGetValue(type, out var existingType)) {
            existingWrapper = existingType;
            return true;
        }

        existingWrapper = null!;
        return false;
    }

    public void Add(Type type, Type wrapper) {
        _wrappedTypes[type] = wrapper;
    }

    public Type GetWrapperOfSelf(Type impl) {
        return TryGetValue(impl, out var existingWrapper) ? existingWrapper : impl;
    }
}