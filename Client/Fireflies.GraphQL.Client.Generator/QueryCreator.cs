using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Client.Generator;

public class QueryCreator : SDLPrinter {
    private int _fieldCounter = 0;

    public string Query { get; private set; }

    public async Task Execute(GraphQLOperationDefinition operationDefinition, GraphQLGeneratorContext context) {
        await using var writer = new StringWriter();
        await PrintAsync(operationDefinition, writer, context.CancellationToken);

        var fragmentVisitor = new FragmentVisitor(this, writer);
        await fragmentVisitor.VisitAsync(operationDefinition, context);
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

    private class FragmentVisitor : ASTVisitor<GraphQLGeneratorContext> {
        private readonly SDLPrinter _sdlPrinter;
        private readonly StringWriter _stringWriter;
        private bool _isInsideIncludedFragmentSpread;
        private HashSet<string> _includedFragments = new();

        public FragmentVisitor(SDLPrinter sdlPrinter, StringWriter stringWriter) {
            _sdlPrinter = sdlPrinter;
            _stringWriter = stringWriter;
        }

        protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, GraphQLGeneratorContext context) {
            _isInsideIncludedFragmentSpread = true;
            var fragment = await context.FragmentAccessor.GetFragment(fragmentSpread.FragmentName).ConfigureAwait(false);
            await VisitAsync(fragment, context);
            _isInsideIncludedFragmentSpread = false;
        }

        protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, GraphQLGeneratorContext context) {
            if(!_isInsideIncludedFragmentSpread)
                return;

            if(!_includedFragments.Add(fragmentDefinition.FragmentName.Name.StringValue))
                return;

            await _stringWriter.WriteLineAsync();
            await _sdlPrinter.PrintAsync(fragmentDefinition, _stringWriter);

            foreach(var selection in fragmentDefinition.SelectionSet.Selections) {
                await VisitAsync(selection, context).ConfigureAwait(false);
            }
        }
    }
}