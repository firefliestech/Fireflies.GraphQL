# Fireflies GraphQL Entity Framework Core extension

## Example
Add the following code to your WebApplication pipeline.
```
var entityFrameworkOptions = graphQLOptions.UseEntityFramework();
entityFrameworkOptions.Register<BloggingContext>();
```

## Operation definition
```
[GraphQLSort]
[GraphQLQuery]
public IQueryable<Blog> Blogs([Resolved] BloggingContext db) {
    return db.Blogs;
}
```

## Model
```
using Microsoft.EntityFrameworkCore;

public class BloggingContext : DbContext {
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    public string DbPath { get; }

    public BloggingContext() {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = System.IO.Path.Join(path, "blogging.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options) {
        options.UseSqlite($"Data Source={DbPath}");
    }
}

public class Blog {
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; }
}

public class Post {
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
}
```

_Logo by freepik_