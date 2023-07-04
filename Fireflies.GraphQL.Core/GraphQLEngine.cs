using Fireflies.GraphQL.Core.Json;
using Fireflies.GraphQL.Core.Scalar;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Exceptions;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public class GraphQLEngine : ASTVisitor<IRequestContext> {
    private readonly GraphQLOptions _options;
    private readonly WrapperRegistry _wrapperRegistry;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly JsonWriterFactory _writerFactory;

    private readonly IConnectionContext _connectionContext;
    private readonly OperationVisitor _operationVisitor;

    public GraphQLEngine(GraphQLOptions options, IConnectionContext connectionContext, WrapperRegistry wrapperRegistry, ScalarRegistry scalarRegistry, JsonWriterFactory writerFactory, OperationVisitor operationVisitor) {
        _options = options;
        _wrapperRegistry = wrapperRegistry;
        _scalarRegistry = scalarRegistry;
        _writerFactory = writerFactory;
        _operationVisitor = operationVisitor;
        _connectionContext = connectionContext;
    }

    public async Task Execute(GraphQLRequest? request, RequestContext requestContext) {
        var (graphQLDocument, result) = Parse(request);
        requestContext.Document = graphQLDocument;
        if(result != null) {
            requestContext.IncreaseExpectedOperations();
            await requestContext.PublishResult(result).ConfigureAwait(false);
            return;
        }

        requestContext.FragmentAccessor = new FragmentAccessor(graphQLDocument!, requestContext);
        requestContext.ValueAccessor = new ValueAccessor(request!.Variables, requestContext);

        var requestValidator = new RequestValidator(request, _options, requestContext, _wrapperRegistry, _scalarRegistry);
        await requestValidator.Validate(graphQLDocument!).ConfigureAwait(false);
        if(requestValidator.Errors.Any()) {
            requestContext.IncreaseExpectedOperations();
            await requestContext.PublishResult(GenerateValidationErrorResult(requestValidator.Errors)).ConfigureAwait(false);
        } else {
            requestContext.Writer = !requestContext.ConnectionContext.IsWebSocket ? _writerFactory.CreateResultWriter() : null;
            await VisitAsync(graphQLDocument, requestContext).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<(string? Id, byte[] Result)> Results() {
        await foreach(var result in _connectionContext.Results.WithCancellation(_connectionContext.CancellationToken).ConfigureAwait(false)) {
            yield return result;

            if(!_connectionContext.IsWebSocket)
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

    private JsonWriter GenerateValidationErrorResult(IEnumerable<IGraphQLError> errors) {
        var errorWriter = _writerFactory.CreateResultWriter();
        foreach(var error in errors)
            errorWriter.AddError(error);
        return errorWriter;
    }

    private JsonWriter GenerateErrorResult(string exceptionMessage, string code) {
        var errorWriter = _writerFactory.CreateResultWriter();
        errorWriter.AddError(code, exceptionMessage);
        return errorWriter;
    }

    protected override async ValueTask VisitOperationDefinitionAsync(GraphQLOperationDefinition operationDefinition, IRequestContext context) {
        if(operationDefinition.Operation is OperationType.Query or OperationType.Mutation)
            context.ConnectionContext.Results.IncreaseExpectedOperations(operationDefinition.SelectionSet.Selections.Count);

        var operationContext = new OperationContext(context, operationDefinition);
        await _operationVisitor.VisitAsync(operationDefinition, operationContext);
    }
}