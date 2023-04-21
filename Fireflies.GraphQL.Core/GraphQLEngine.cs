using Fireflies.GraphQL.Core.Json;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.IoC.Abstractions;
using Fireflies.Logging.Abstractions;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Exceptions;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Core;

public class GraphQLEngine : ASTVisitor<RequestContext> {
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly WrapperRegistry _wrapperRegistry;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly IFirefliesLoggerFactory _loggerFactory;
    private FragmentAccessor _fragmentAccessor = null!;
    private ValueAccessor _valueAccessor = null!;

    private IConnectionContext _connectionContext;

    private JsonWriter? _writer;

    public GraphQLEngine(GraphQLOptions options, IDependencyResolver dependencyResolver, IConnectionContext connectionContext, WrapperRegistry wrapperRegistry, ScalarRegistry scalarRegistry, IFirefliesLoggerFactory loggerFactory) {
        _options = options;
        _dependencyResolver = dependencyResolver;
        _wrapperRegistry = wrapperRegistry;
        _scalarRegistry = scalarRegistry;
        _loggerFactory = loggerFactory;
        _connectionContext = connectionContext;
    }

    public async Task Execute(GraphQLRequest? request, RequestContext requestContext) {
        var (graphQLDocument, result) = Parse(request);
        if(result != null) {
            requestContext.IncreaseExpectedOperations();
            await requestContext.PublishResult(result).ConfigureAwait(false);
            return;
        }

        _fragmentAccessor = new FragmentAccessor(graphQLDocument!, requestContext);
        _valueAccessor = new ValueAccessor(request!.Variables, requestContext);

        var validationErrors = await new RequestValidator(request, _fragmentAccessor, _options, _dependencyResolver, requestContext, _wrapperRegistry, _scalarRegistry, _valueAccessor).Validate(graphQLDocument!).ConfigureAwait(false);
        if(validationErrors.Any()) {
            requestContext.IncreaseExpectedOperations();
            await requestContext.PublishResult(GenerateValidationErrorResult(validationErrors)).ConfigureAwait(false);
        } else {
            _writer = !requestContext.ConnectionContext.IsWebSocket ? new JsonWriter(_scalarRegistry) : null;
            await VisitAsync(graphQLDocument, requestContext).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<(string Id, byte[] Result)> Results() {
        await foreach(var result in _connectionContext.WithCancellation(_connectionContext.CancellationToken).ConfigureAwait(false)) {
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
    
    protected override async ValueTask VisitOperationDefinitionAsync(GraphQLOperationDefinition operationDefinition, RequestContext context) {
        if(operationDefinition.Operation is OperationType.Query or OperationType.Mutation)
            context.ConnectionContext.IncreaseExpectedOperations(operationDefinition.SelectionSet.Selections.Count);

        var visitor = new OperationVisitor(_options, _dependencyResolver, _fragmentAccessor, _valueAccessor, _wrapperRegistry, operationDefinition.Operation, context, _scalarRegistry, _writer, _loggerFactory);
        await visitor.VisitAsync(operationDefinition, context).ConfigureAwait(false);
    }
}