using Fireflies.GraphQL.Abstractions.Generator;

namespace Fireflies.GraphQL.Core.Generators.Connection;

public abstract class ConnectionBase {

}

[GraphQLNoWrapper]
public class ConnectionBase<TEdge, TNode> : ConnectionBase where TEdge : EdgeBase<TNode> {
    private readonly IEnumerable<TEdge> _filteredEdges;

    protected ConnectionBase(IEnumerable<TEdge> edges, int first, string? after) {
        if(first <= 0)
            first = 1;

        TotalCount = edges.Count();

        _filteredEdges = edges;
        if(after != null)
            _filteredEdges = _filteredEdges.SkipWhile(x => x.Cursor != after).Skip(1);
        _filteredEdges = _filteredEdges.Take(first);

        var lastFilteredEdge = _filteredEdges.LastOrDefault();
        var lastEdge = edges.LastOrDefault();
        PageInfo = new PageInfo {
            EndCursor = lastFilteredEdge?.Cursor ?? null,
            HasNextPage = lastEdge != null && lastFilteredEdge != lastEdge
        };
    }

    public int TotalCount { get; }
    public IEnumerable<TEdge> Edges => _filteredEdges;
    public PageInfo PageInfo { get; }
}