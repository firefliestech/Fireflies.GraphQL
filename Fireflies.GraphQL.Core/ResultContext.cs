namespace Fireflies.GraphQL.Core;

public class ResultContext {
    private readonly Stack<Type> _types = new();

    internal ResultContext Push(Type type) {
        _types.Push(type);
        return this;
    }

    internal void Pop() {
        _types.Pop();
    }

    public bool Any(Func<Type, bool> predicate) {
        return _types.Any(predicate);
    } 
}