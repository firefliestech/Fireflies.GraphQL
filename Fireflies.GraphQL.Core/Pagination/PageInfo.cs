namespace Fireflies.GraphQL.Core.Pagination;

public class PageInfo {
    public string? EndCursor { get; set; }
    public bool HasNextPage { get; set; }
}