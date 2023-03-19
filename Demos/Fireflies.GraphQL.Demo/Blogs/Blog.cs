using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo.Blogs;

public class Blog {
    [GraphQLId]
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; }
}