using System.Runtime.CompilerServices;

namespace Fireflies.GraphQL.Core;

public static class WrapperHelper {
    public static async IAsyncEnumerable<TWrapped?> WrapAsyncEnumerableResult<TWrapped, TOriginal>(IAsyncEnumerable<TOriginal> result, [EnumeratorCancellation] CancellationToken cancellationToken, WrapperRegistry wrapperRegistry) {
        await foreach(var entry in result.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            yield return WrapResult<TWrapped, TOriginal>(entry, wrapperRegistry);
        }
    }

    // Project type to wrapper
    public static Task<IQueryable<TWrapped?>?>? WrapQueryableTaskResult<TWrapped, TOriginal>(Task<IQueryable<TOriginal>?> result, WrapperRegistry wrapperRegistry) {
        return result?.ContinueWith(task => WrapQueryableResult<TWrapped, TOriginal>(task.Result, wrapperRegistry));
    }

    public static IQueryable<TWrapped?>? WrapQueryableResult<TWrapped, TOriginal>(IQueryable<TOriginal>? result, WrapperRegistry wrapperRegistry) {
        return result?.Select(x => WrapResult<TWrapped, TOriginal>(x, wrapperRegistry));
    }

    public static Task<IEnumerable<TWrapped?>?>? WrapEnumerableTaskResult<TWrapped, TOriginal>(Task<IEnumerable<TOriginal>?> result, WrapperRegistry wrapperRegistry) {
        return result?.ContinueWith(task => WrapEnumerableResult<TWrapped, TOriginal>(task.Result, wrapperRegistry));
    }

    public static IEnumerable<TWrapped?>? WrapEnumerableResult<TWrapped, TOriginal>(IEnumerable<TOriginal>? result, WrapperRegistry wrapperRegistry) {
        return result?.Select(x => WrapResult<TWrapped, TOriginal>(x, wrapperRegistry));
    }

    public static Task<TWrapped?> WrapTaskResult<TWrapped, TOriginal>(Task<TOriginal> result, WrapperRegistry wrapperRegistry) {
        return result.ContinueWith(task => WrapResult<TWrapped, TOriginal>(task.Result, wrapperRegistry));
    }

    public static TWrapped? WrapResult<TWrapped, TOriginal>(TOriginal? r, WrapperRegistry wrapperRegistry) {
        if(r == null)
            return default;

        var wrappedType = typeof(TWrapped);
        if(wrappedType.IsInterface) {
            var implType = wrapperRegistry.GetWrapperOfSelf(r.GetType());
            return (TWrapped)Activator.CreateInstance(implType, r)!;
        }

        if(wrappedType == typeof(TOriginal))
            return (TWrapped?)(object?)r;

        return (TWrapped)Activator.CreateInstance(typeof(TWrapped), r)!;
    }

    // Connection
    public static Task<TConnection> CreateEnumerableTaskConnection<TConnection, TEdge, TNode>(Task<IEnumerable<TNode>> nodesTask, int first, string after) {
        return nodesTask.ContinueWith(r => CreateEnumerableConnection<TConnection, TEdge, TNode>(r.Result, first, after));
    }

    public static TConnection CreateEnumerableConnection<TConnection, TEdge, TNode>(IEnumerable<TNode> nodes, int first, string after) {
        var edges = nodes.Select(n => (TEdge)Activator.CreateInstance(typeof(TEdge), n)!).ToArray();
        var type = typeof(TConnection);
        return (TConnection)Activator.CreateInstance(type, edges, first, after)!;
    }
}