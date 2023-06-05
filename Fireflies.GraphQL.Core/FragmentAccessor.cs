using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public class FragmentAccessor {
    private readonly GraphQLDocument _document;
    private readonly IRequestContext _context;
    private Dictionary<string, GraphQLFragmentDefinition>? _fragments;
    
    private readonly SemaphoreSlim _semaphore = new(1);

    private static readonly FragmentVisitor FragmentVisitorInstance;

    static FragmentAccessor() {
        FragmentVisitorInstance = new FragmentVisitor();
    }

    public FragmentAccessor(GraphQLDocument document, IRequestContext context) {
        _document = document;
        _context = context;
    }

    public async Task<GraphQLFragmentDefinition> GetFragment(GraphQLFragmentName fragmentName) {
        return await GetFragment(fragmentName.Name.StringValue);
    }

    public async Task<GraphQLFragmentDefinition> GetFragment(string name) {
        if(_fragments != null)
            return _fragments[name];

        try {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            if(_fragments == null) {
                var context = new FragmentVisitorContext(_context);
                await FragmentVisitorInstance.VisitAsync(_document, context).ConfigureAwait(false);
                _fragments = context.FragmentDefinitions.ToDictionary(x => x.FragmentName.Name.StringValue);
            }
        } finally {
            _semaphore.Release();
        }

        return _fragments[name];
    }

    private class FragmentVisitor : ASTVisitor<FragmentVisitorContext> {
        protected override ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, FragmentVisitorContext context) {
            context.FragmentDefinitions.Add(fragmentDefinition);
            return ValueTask.CompletedTask;
        }
    }

    private class FragmentVisitorContext : IASTVisitorContext {
        private readonly IRequestContext _context;

        public FragmentVisitorContext(IRequestContext context) {
            _context = context;
        }

        public List<GraphQLFragmentDefinition> FragmentDefinitions { get; } = new();
        public CancellationToken CancellationToken => _context.CancellationToken;
    }
}