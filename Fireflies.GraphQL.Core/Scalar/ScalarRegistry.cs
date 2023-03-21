namespace Fireflies.GraphQL.Core.Scalar;

public class ScalarRegistry {
    private readonly Dictionary<Type, IScalarHandler> _handlers = new();

    public void AddScalar<T>(IScalarHandler handler) {
        _handlers.Add(typeof(T), handler);
    }

    public void AddScalar(Type type, IScalarHandler handler) {
        _handlers.Add(type, handler);
    }

    public bool IsValidGraphQLObjectType(Type type) {
        return Type.GetTypeCode(type) == TypeCode.Object && !_handlers.ContainsKey(type);
    }

    public bool Contains(Type type) {
        return _handlers.ContainsKey(type);
    }

    public bool NameToType(string? typeName, out Type? type) {
        foreach(var x in _handlers.Where(x => x.Key.Name.Equals(typeName, StringComparison.InvariantCultureIgnoreCase))) {
            type = x.Key;
            return true;
        }

        type = null;
        return false;
    }

    public bool GetHandler(Type memberInfo, out IScalarHandler? handler) {
        if(_handlers.TryGetValue(memberInfo, out var value)) {
            handler = value;
            return true;
        }

        handler = null;
        return false;
    }
}