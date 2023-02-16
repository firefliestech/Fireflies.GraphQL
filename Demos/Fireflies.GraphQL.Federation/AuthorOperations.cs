using Fireflies.GraphQL.Contract;
using Fireflies.GraphQL.Core;

namespace Fireflies.GraphQL.FederationDemo;

public class AuthorOperations {
    private readonly GraphQLContext _context;

    public AuthorOperations(GraphQLContext context) {
        _context = context;
    }

    [GraphQLQuery]
    public Task<IAuthor> Author(int authorId = 10, string filter = null) {
        var author = new Author { Id = authorId, Name = "Lars" };
        return Task.FromResult<IAuthor>(author);
    }

    [GraphQLQuery]
    public Task<IEnumerable<Author>> Authors() {
        var author1 = new Author { Id = 101, Name = "Lars", Emails = new[] { "kalle@abc.com", "banan@abc.com" } };
        var author2 = new Author { Id = 102, Name = "Kalle", Emails = new[] { "kalle@abc.com" }};
        return Task.FromResult((IEnumerable<Author>)new[] { author1, author2 });
    }
}