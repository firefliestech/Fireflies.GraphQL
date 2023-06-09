using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core;

internal class GraphQLPath : IGraphQLPath {
    public IEnumerable<object> Path { get; }

    public IGraphQLPath Add(object subPath) {
        return new GraphQLPath(this, subPath);
    }

    private GraphQLPath(GraphQLPath parent, object subPath) {
        var value = subPath;
        if(subPath is string s)
            value = s.LowerCaseFirstLetter();

        Path = parent.Path.Union(new[] { value });
    }

    internal GraphQLPath(Stack<object> path) {
        Path = path.Select(x => x).Reverse().ToList();
    }
}