using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Client.Generator;

public class QueryCreator : SDLPrinter {
    private readonly GraphQLDocument _currentDocument;
    private readonly HashSet<string> _includedFragments = new();

    private int _fieldCounter = 0;
    
    public QueryCreator(GraphQLDocument currentDocument) {
        _currentDocument = currentDocument;
    }

    public string Query { get; private set; }

    public async Task Execute(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        await using var writer = new StringWriter();
        await PrintAsync(operationDefinition, writer, context.CancellationToken);

        var fragmentVisitor = new FragmentVisitor(_includedFragments, this, writer);
        await fragmentVisitor.VisitAsync(_currentDocument, context);
        Query = writer.ToString();
    }

    protected override async ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, DefaultPrintContext context) {
        if(selectionSet.Selections.Any(x => x.Kind == ASTNodeKind.Field && ((GraphQLField)x).Name.StringValue == "__typename")) {
            await base.VisitSelectionSetAsync(selectionSet, context).ConfigureAwait(false);
            return;
        }

        if(_fieldCounter > 0) {
            var graphQLField = new GraphQLField(new GraphQLName("__typename"));
            selectionSet.Selections.Add(graphQLField);
        }

        await base.VisitSelectionSetAsync(selectionSet, context).ConfigureAwait(false);
    }

    protected override async ValueTask VisitFieldAsync(GraphQLField field, DefaultPrintContext context) {
        _fieldCounter++;
        await base.VisitFieldAsync(field, context);
        _fieldCounter--;
    }

    protected override ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, DefaultPrintContext context) {
        _includedFragments.Add(fragmentSpread.FragmentName.Name.StringValue);
        return base.VisitFragmentSpreadAsync(fragmentSpread, context);
    }

    private class FragmentVisitor : ASTVisitor<GraphQLGeneratorContext> {
        private readonly HashSet<string> _graphQLFragmentNames;
        private readonly SDLPrinter _sdlPrinter;
        private readonly StringWriter _stringWriter;

        public FragmentVisitor(HashSet<string> graphQLFragmentNames, SDLPrinter sdlPrinter, StringWriter stringWriter) {
            _graphQLFragmentNames = graphQLFragmentNames;
            _sdlPrinter = sdlPrinter;
            _stringWriter = stringWriter;
        }

        protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, GraphQLGeneratorContext context) {
            if(_graphQLFragmentNames.Contains(fragmentDefinition.FragmentName.Name.StringValue)) {
                await _stringWriter.WriteLineAsync();
                await _sdlPrinter.PrintAsync(fragmentDefinition, _stringWriter);
            }
        }
    }
}