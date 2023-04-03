using Fireflies.GraphQL.Core.Json;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.IoC.Abstractions;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Exceptions;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public class GraphQLEngine : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly WrapperRegistry _wrapperRegistry;
    private readonly ScalarRegistry _scalarRegistry;
    private FragmentAccessor _fragmentAccessor = null!;
    private ValueAccessor _valueAccessor = null!;

    public IGraphQLContext Context { get; }

    private JsonWriter? _writer;

    public GraphQLEngine(GraphQLOptions options, IDependencyResolver dependencyResolver, IGraphQLContext context, WrapperRegistry wrapperRegistry, ScalarRegistry scalarRegistry) {
        _options = options;
        _dependencyResolver = dependencyResolver;
        _wrapperRegistry = wrapperRegistry;
        _scalarRegistry = scalarRegistry;
        Context = context;
    }

    public async Task Execute(GraphQLRequest? request) {
        var (graphQLDocument, result) = Parse(request);
        if(result != null) {
            Context.IncreaseExpectedOperations();
            Context.PublishResult(result);
            return;
        }

        _fragmentAccessor = new FragmentAccessor(graphQLDocument!, Context);
        _valueAccessor = new ValueAccessor(request!.Variables, Context);

        var validationErrors = await new RequestValidator(request, _fragmentAccessor, _options, _dependencyResolver, Context, _wrapperRegistry, _scalarRegistry, _valueAccessor).Validate(graphQLDocument!).ConfigureAwait(false);
        if(validationErrors.Any()) {
            Context.IncreaseExpectedOperations();
            Context.PublishResult(GenerateValidationErrorResult(validationErrors));
        } else {
            _writer = !Context.IsWebSocket ? new JsonWriter(_scalarRegistry) : null;
            await VisitAsync(graphQLDocument, Context).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<byte[]> Results() {
        await foreach(var result in Context.WithCancellation(Context.CancellationToken).ConfigureAwait(false)) {
            yield return result;

            if(!Context.IsWebSocket)
                yield break;
        }
    }

    private (GraphQLDocument?, JsonWriter?) Parse(GraphQLRequest? request) {
        if(request?.Query == null) {
            return (null, GenerateErrorResult("Empty request", "GRAPHQL_SYNTAX_ERROR"));
        }

        try {
            return (Parser.Parse(request.Query, new ParserOptions { Ignore = IgnoreOptions.All }), null);
        } catch(GraphQLSyntaxErrorException sex) {
            return (null, GenerateErrorResult(sex.Description, "GRAPHQL_SYNTAX_ERROR"));
        }
    }

    private JsonWriter GenerateValidationErrorResult(List<string> errors) {
        var errorWriter = new JsonWriter(_scalarRegistry);
        foreach(var error in errors)
            errorWriter.AddError(error, "GRAPHQL_VALIDATION_FAILED");
        return errorWriter;
    }

    private JsonWriter GenerateErrorResult(string exceptionMessage, string code) {
        var errorWriter = new JsonWriter(_scalarRegistry);
        errorWriter.AddError(exceptionMessage, code);
        return errorWriter;
    }
    
    protected override async ValueTask VisitOperationDefinitionAsync(GraphQLOperationDefinition operationDefinition, IGraphQLContext context) {
        if(operationDefinition.Operation is OperationType.Query or OperationType.Mutation)
            context.IncreaseExpectedOperations(operationDefinition.SelectionSet.Selections.Count);

        var visitor = new OperationVisitor(_options, _dependencyResolver, _fragmentAccessor, _valueAccessor, _wrapperRegistry, operationDefinition.Operation, context, _scalarRegistry, _writer);
        await visitor.VisitAsync(operationDefinition, context).ConfigureAwait(false);
    }
}