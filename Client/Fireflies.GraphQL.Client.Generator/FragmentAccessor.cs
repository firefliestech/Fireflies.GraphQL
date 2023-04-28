using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Client.Generator;

public class FragmentAccessor {
    private readonly GraphQLDocument _document;
    private Dictionary<string, GraphQLFragmentDefinition>? _fragments;

    private static readonly FragmentVisitor FragmentVisitorInstance;

    static FragmentAccessor() {
        FragmentVisitorInstance = new FragmentVisitor();
    }

    public FragmentAccessor(GraphQLDocument document) {
        _document = document;
    }

    public async Task<GraphQLFragmentDefinition> GetFragment(GraphQLFragmentName fragmentName) {
        var fragments = await LoadFragments();
        return fragments[fragmentName.Name.StringValue];
    }

    private async Task<Dictionary<string, GraphQLFragmentDefinition>> LoadFragments() {
        if(_fragments == null) {
            var context = new FragmentVisitorContext();
            await FragmentVisitorInstance.VisitAsync(_document, context).ConfigureAwait(false);
            _fragments = context.FragmentDefinitions.ToDictionary(x => x.FragmentName.Name.StringValue);
        }

        return _fragments;
    }

    private class FragmentVisitor : ASTVisitor<FragmentVisitorContext> {
#pragma warning disable CS1998
        protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, FragmentVisitorContext context) {
            context.FragmentDefinitions.Add(fragmentDefinition);
        }
#pragma warning restore CS1998
    }

    private class FragmentVisitorContext : IASTVisitorContext {
        public List<GraphQLFragmentDefinition> FragmentDefinitions { get; } = new();
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    public async Task<IEnumerable<GraphQLFragmentDefinition>> GetAll() {
        var fragments = await LoadFragments();
        return fragments.Values;
    }
}