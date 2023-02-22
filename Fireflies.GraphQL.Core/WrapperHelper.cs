using System.Runtime.CompilerServices;

namespace Fireflies.GraphQL.Core;

public static class WrapperHelper {
    public static async IAsyncEnumerable<TWrapped> WrapAsyncEnumerableResult<TWrapped, TOriginal>(IAsyncEnumerable<TOriginal> result, [EnumeratorCancellation]CancellationToken cancellationToken) {
        await foreach(var entry in result.WithCancellation(cancellationToken)) {
            yield return WrapResult<TWrapped, TOriginal>(entry);
        }
    }

    public static Task<IEnumerable<TWrapped>> WrapEnumerableTaskResult<TWrapped, TOriginal>(Task<IEnumerable<TOriginal>> result) {
        return result.ContinueWith(r => WrapEnumerableResult<TWrapped, TOriginal>(r.Result));
    }

    public static IEnumerable<TWrapped>? WrapEnumerableResult<TWrapped, TOriginal>(IEnumerable<TOriginal>? result) {
        return result?.Select(x => {
            var instance = WrapResult<TWrapped, TOriginal>(x);
            return instance;
        });
    }

    public static Task<TWrapped> WrapTaskResult<TWrapped, TOriginal>(Task<TOriginal> result) {
        return result.ContinueWith(task => WrapResult<TWrapped, TOriginal>(task.Result));
    }

    public static TWrapped WrapResult<TWrapped, TOriginal>(TOriginal r) {
        return (TWrapped)Activator.CreateInstance(typeof(TWrapped), r)!;
    }

    public static Task<TConnection> CreateEnumerableTaskConnection<TConnection, TEdge, TNode>(Task<IEnumerable<TNode>> nodesTask, int first, string after) {
        return nodesTask.ContinueWith(r => CreateEnumerableConnection<TConnection, TEdge, TNode>(r.Result, first, after));
    }

    public static TConnection CreateEnumerableConnection<TConnection, TEdge, TNode>(IEnumerable<TNode> nodes, int first, string after) {
        var edges = nodes.Select(n => (TEdge)Activator.CreateInstance(typeof(TEdge), n)!).ToArray();
        var type = typeof(TConnection);
        return (TConnection)Activator.CreateInstance(type, edges, first, after)!;
    }
}