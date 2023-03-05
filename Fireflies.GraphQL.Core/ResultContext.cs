namespace Fireflies.GraphQL.Core;

public class ResultContext {
    private readonly Stack<Entry> _types = new();

    internal ResultContext Push(Type type, object? data = null) {
        _types.Push(new Entry(data, type, _types.Count));
        return this;
    }

    internal Entry Pop() {
        return _types.Pop();
    }

    public bool Any(Func<Type, bool> predicate) {
        return _types.Select(x => x.Type).Any(predicate);
    }

    internal Entry Peek() {
        return _types.Peek();
    }

    public class Entry {
        private readonly HashSet<string> _addedFields = new();

        public object? Data { get; }
        public int SubLevel { get; }
        public Type Type { get; }

        public Entry(object? data, Type type, int subLevel) {
            SubLevel = subLevel;
            Data = data;
            Type = type;
        }

        public bool ShouldAdd(string name) {
            return _addedFields.Add(name);
        }
    }
}