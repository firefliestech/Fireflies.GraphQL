using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Connection;

namespace Fireflies.GraphQL.Demos.Server.Authors.Authors;

public class AuthorOperations {
    [GraphQLQuery]
    public Task<IAuthor> Author(int authorId = 10) {
        if(authorId > 100)
            return Task.FromResult<IAuthor>(new PseudnonymAuthor { Id = authorId, Name = "Calvin Smallhead" });

        return Task.FromResult<IAuthor>(new RealAuthor { Id = authorId, Name = "Calvin Little", Emails = new[] { "calvin.little@fireflies.tech" } });
    }

    [GraphQLQuery]
    [GraphQLPagination]
    public Task<IEnumerable<IAuthor>> Authors(AuthorsFilter? filter) {
        var list = new List<IAuthor>();
        for(var i = 0; i < 1000; i++) {
            list.Add(new RealAuthor { Id = i, Name = $"Calvin the {i} Little", Emails = new[] { "calvin.little@fireflies.tech", "cl@fireflies.tech" } });
        }

        return Task.FromResult(list.AsEnumerable());
    }

    [GraphQLMutation]
    [MustBeAllowedToUpdateAuthor]
    public Task<IAuthor> UpdateAuthor(UpdateAuthorInput input) {
        return Task.FromResult((IAuthor)new RealAuthor { Id = input.AuthorId, Name = input.Name, Emails = Array.Empty<string>() });
    }

    [GraphQLSubscription]
    public async IAsyncEnumerable<IAuthor> AuthorAdded([EnumeratorCancellation] CancellationToken cancellationToken = default) {
        while(!cancellationToken.IsCancellationRequested) {
            await Task.Delay(7500, cancellationToken).ConfigureAwait(false);
            yield return new RealAuthor { Id = DateTime.UtcNow.Second, Name = "Lars" };
        }
    }
}