using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Connection;
using Fireflies.GraphQL.Abstractions.Sorting;
using Fireflies.GraphQL.Abstractions.Where;
using Fireflies.GraphQL.Core;

namespace Fireflies.GraphQL.Demo.Blogs;

public class BlogOperations {
    [GraphQLQuery]
    [GraphQLSort]
    [GraphQLPagination]
    [GraphQLWhere]
    public IQueryable<Blog> Blogs([Resolved] BloggingContext db) {
        return db.Blogs;
    }
}