using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.FederationDemo;

public class AuthorOperations {
    [GraphQLQuery]
    public Task<IAuthor> Author(int authorId = 10) {
        if(authorId > 100)
            return Task.FromResult<IAuthor>(new PseudnonymAuthor { Id = authorId, Name = "Lars" });

        return Task.FromResult<IAuthor>(new RealAuthor { Id = authorId, Name = "Lars", Emails = new[] { "kalle@abc.com" } });
    }

    [GraphQLQuery]
    public Task<IEnumerable<RealAuthor>> Authors() {
        var author1 = new RealAuthor { Id = 101, Name = "Lars", Emails = new[] { "kalle@abc.com", "banan@abc.com" } };
        var author2 = new RealAuthor { Id = 102, Name = "Kalle", Emails = new[] { "kalle@abc.com" } };
        return Task.FromResult((IEnumerable<RealAuthor>)new[] { author1, author2 });
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<RealAuthor> AuthorAdded([EnumeratorCancellation] CancellationToken cancellationToken = default) {
        while(!cancellationToken.IsCancellationRequested) {
            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            yield return new RealAuthor { Id = DateTime.UtcNow.Second, Name = "Lars" };
        }
    }
}