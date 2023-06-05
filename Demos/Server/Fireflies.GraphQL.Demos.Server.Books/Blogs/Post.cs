namespace Fireflies.GraphQL.Demos.Server.Books.Blogs;

public class Post {
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
}