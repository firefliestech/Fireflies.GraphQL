using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demos.Server.Books.Blogs;

public class Blog {
    [GraphQLId]
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; }
}