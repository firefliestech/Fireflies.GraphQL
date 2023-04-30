namespace Fireflies.GraphQL.Abstractions;

/// <summary>Will make the result builder iterate the items in parallel.</summary>
/// <remarks>
/// Should only be used on fields with a significant IO/processing time. On simple operations this attribute usually causes execution to be slower.
/// </remarks>
public class GraphQLParallel : GraphQLAttribute {
    public bool SortResults { get; set; }

    public GraphQLParallel(bool sortResults = false) {
        SortResults = sortResults;
    }
}
