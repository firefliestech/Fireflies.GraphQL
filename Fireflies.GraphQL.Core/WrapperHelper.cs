using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fireflies.GraphQL.Core;

public static class WrapperHelper {
    public static async IAsyncEnumerable<TWrapped?> WrapAsyncEnumerableResult<TWrapped, TOriginal>(IAsyncEnumerable<TOriginal> result, [EnumeratorCancellation] CancellationToken cancellationToken, WrapperRegistry wrapperRegistry) {
        await foreach(var entry in result.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            yield return WrapResult<TWrapped, TOriginal>(entry, wrapperRegistry);
        }
    }

    // Project type to wrapper
    public static async Task<IQueryable<TWrapped?>?>? WrapQueryableTaskResult<TWrapped, TOriginal>(Task<IQueryable<TOriginal>?> result, WrapperRegistry wrapperRegistry) {
        var taskResult = await result.ConfigureAwait(false);
        return WrapQueryableResult<TWrapped, TOriginal>(taskResult, wrapperRegistry);
    }

    public static IQueryable<TWrapped?>? WrapQueryableResult<TWrapped, TOriginal>(IQueryable<TOriginal>? result, WrapperRegistry wrapperRegistry) {
        return result?.Select(x => WrapResult<TWrapped, TOriginal>(x, wrapperRegistry));
    }

    public static async Task<IEnumerable<TWrapped?>?>? WrapEnumerableTaskResult<TWrapped, TOriginal>(Task<IEnumerable<TOriginal>?> result, WrapperRegistry wrapperRegistry) {
        var taskResult = await result.ConfigureAwait(false);
        return WrapEnumerableResult<TWrapped, TOriginal>(taskResult, wrapperRegistry);
    }

    public static IEnumerable<TWrapped?>? WrapEnumerableResult<TWrapped, TOriginal>(IEnumerable<TOriginal>? result, WrapperRegistry wrapperRegistry) {
        return result?.Select(x => WrapResult<TWrapped, TOriginal>(x, wrapperRegistry));
    }

    public static async Task<TWrapped?> WrapTaskResult<TWrapped, TOriginal>(Task<TOriginal> result, WrapperRegistry wrapperRegistry) {
        var taskResult = await result.ConfigureAwait(false);
        return WrapResult<TWrapped, TOriginal>(taskResult, wrapperRegistry);
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
    public static async Task<TConnection> CreateEnumerableTaskConnection<TConnection, TEdge, TNode>(Task<IEnumerable<TNode>> nodesTask, int first, string after) {
        var taskResult = await nodesTask.ConfigureAwait(false);
        return CreateEnumerableConnection<TConnection, TEdge, TNode>(taskResult, first, after);
    }

    public static TConnection CreateEnumerableConnection<TConnection, TEdge, TNode>(IEnumerable<TNode> nodes, int first, string after) {
        var edges = nodes.Select(n => (TEdge)Activator.CreateInstance(typeof(TEdge), n)!).ToArray();
        var type = typeof(TConnection);
        return (TConnection)Activator.CreateInstance(type, edges, first, after)!;
    }
}