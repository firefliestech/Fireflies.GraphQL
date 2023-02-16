using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

internal class FragmentAccessor {
    private readonly GraphQLDocument _document;
    private readonly GraphQLContext _context;
    private Dictionary<string, GraphQLFragmentDefinition>? _fragments;

    private static readonly FragmentVisitor FragmentVisitorInstance;

    static FragmentAccessor() {
        FragmentVisitorInstance = new FragmentVisitor();
    }

    public FragmentAccessor(GraphQLDocument document, GraphQLContext context) {
        _document = document;
        _context = context;
    }

    public async Task<GraphQLFragmentDefinition> GetFragment(GraphQLFragmentName fragmentName) {
        if(_fragments == null) {
            var context = new FragmentVisitorContext(_context);
            await FragmentVisitorInstance.VisitAsync(_document, context);
            _fragments = context.FragmentDefinitions.ToDictionary(x => x.FragmentName.Name.StringValue);
        }

        return _fragments[fragmentName.Name.StringValue];
    }

    private class FragmentVisitor : ASTVisitor<FragmentVisitorContext> {
        protected override ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, FragmentVisitorContext context) {
            context.FragmentDefinitions.Add(fragmentDefinition);
            return ValueTask.CompletedTask;
        }
    }

    private class FragmentVisitorContext : IASTVisitorContext {
        private readonly GraphQLContext _context;

        public FragmentVisitorContext(GraphQLContext context) {
            _context = context;
        }

        public List<GraphQLFragmentDefinition> FragmentDefinitions { get; } = new();
        public CancellationToken CancellationToken => _context.CancellationToken;
    }
}