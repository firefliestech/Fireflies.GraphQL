using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core;

namespace Fireflies.GraphQL.FederationDemo;

public class AuthorOperations {
    private readonly IGraphQLContext _context;

    public AuthorOperations(IGraphQLContext context) {
        _context = context;
    }

    [GraphQLQuery]
    public Task<IAuthor> Author(int authorId = 10, string? filter = null) {
        var author = new RealAuthor { Id = authorId, Name = "Lars" };
        return Task.FromResult<IAuthor>(author);
    }

    [GraphQLQuery]
    public Task<IEnumerable<RealAuthor>> Authors() {
        var author1 = new RealAuthor { Id = 101, Name = "Lars", Emails = new[] { "kalle@abc.com", "banan@abc.com" } };
        var author2 = new RealAuthor { Id = 102, Name = "Kalle", Emails = new[] { "kalle@abc.com" } };
        return Task.FromResult((IEnumerable<RealAuthor>)new[] { author1, author2 });
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<RealAuthor> AuthorAdded([Resolved][EnumeratorCancellation]CancellationToken cancellation) {
        while(!_context.CancellationToken.IsCancellationRequested) {
            await Task.Delay(2000, cancellation);
            yield return new RealAuthor { Id = DateTime.UtcNow.Second, Name = "Lars" };
        }
    }
}