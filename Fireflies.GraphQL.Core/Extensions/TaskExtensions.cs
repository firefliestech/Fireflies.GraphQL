using System.Collections.Concurrent;
using System.Reflection;

namespace Fireflies.GraphQL.Core.Extensions;

internal static class TaskExtensions {
    private static readonly ConcurrentDictionary<Type, MethodInfo> ResultGetMethods = new();

    public static object? GetResult(this Task task) {
        var type = task.GetType();
        var method = ResultGetMethods.GetOrAdd(type, _ => type.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)!.GetMethod!);
        return method.Invoke(task, Array.Empty<object>());
    }
}
