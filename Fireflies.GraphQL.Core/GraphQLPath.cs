using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core;

internal class GraphQLPath : IGraphQLPath {
    public IEnumerable<object> Path { get; }

    public IGraphQLPath Add(params object[] subPath) {
        return new GraphQLPath(this, subPath);
    }

    private GraphQLPath(GraphQLPath parent, object[] subPath) {
        for(var i = 0; i < subPath.Length; i++) {
            if(subPath[i] is string s)
                subPath[i] = s.LowerCaseFirstLetter();
        }

        Path = parent.Path.Union(subPath);
    }

    internal GraphQLPath(Stack<object> path) {
        Path = path.Select(x => x).Reverse().ToList();
    }
}