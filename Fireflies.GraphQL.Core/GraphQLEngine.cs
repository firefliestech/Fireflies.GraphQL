using Fireflies.IoC.Abstractions;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Exceptions;
using GraphQLParser.Visitors;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Fireflies.GraphQL.Core;

public class GraphQLEngine : ASTVisitor<IGraphQLContext> {
    private readonly GraphQLOptions _options;
    private readonly IDependencyResolver _dependencyResolver;
    private FragmentAccessor _fragmentAccessor = null!;
    private ValueAccessor _valueAccessor = null!;

    public IGraphQLContext Context { get; }

    private static readonly JsonSerializerSettings JsonSerializerSettings = new() { Converters = new List<JsonConverter> { new Newtonsoft.Json.Converters.StringEnumConverter() }, NullValueHandling = NullValueHandling.Include };

    public GraphQLEngine(GraphQLOptions options, IDependencyResolver dependencyResolver, IGraphQLContext context) {
        _options = options;
        _dependencyResolver = dependencyResolver;
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

        var errors = await new RequestValidator(request, _fragmentAccessor, _options, _dependencyResolver, Context).Validate(graphQLDocument!).ConfigureAwait(false);
        if(errors.Any()) {
            Context.IncreaseExpectedOperations();
            Context.PublishResult(GenerateValidationErrorResult(errors));
        } else {
            await VisitAsync(graphQLDocument, Context).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<string> Results() {
        if(Context.IsWebSocket) {
            await foreach(var subResult in Context.WithCancellation(Context.CancellationToken).ConfigureAwait(false)) {
                var result = new JObject(new JProperty("data", subResult));
                yield return JsonConvert.SerializeObject(result, JsonSerializerSettings);
            }
        } else {
            var result = new JObject();
            var data = new JObject();
            result.Add("data", data);
            await foreach(var subResult in Context.WithCancellation(Context.CancellationToken).ConfigureAwait(false)) {
                data.Merge(subResult);
            }

            yield return JsonConvert.SerializeObject(result, JsonSerializerSettings);
        }
    }

    private (GraphQLDocument?, JObject?) Parse(GraphQLRequest? request) {
        if(request == null)
            return (null, new JObject(GenerateErrorResult("Empty request", "GRAPHQL_SYNTAX_ERROR")));

        try {
            return (Parser.Parse(request.Query, new ParserOptions { Ignore = IgnoreOptions.All }), null);
        } catch(GraphQLSyntaxErrorException sex) {
            return (null, new JObject(GenerateErrorResult(sex.Description, "GRAPHQL_SYNTAX_ERROR")));
        }
    }

    private JObject GenerateValidationErrorResult(List<string> errors) {
        return new JObject(new JProperty("errors", new JArray(errors.Select(error => GenerateMessage(error, "GRAPHQL_VALIDATION_FAILED")))));
    }

    private JProperty GenerateErrorResult(string exceptionMessage, string code) {
        return new JProperty("errors", new JArray(new object[] {
            GenerateMessage(exceptionMessage, code)
        }));
    }

    private static JObject GenerateMessage(string exceptionMessage, string code) {
        return new JObject {
            { "message", exceptionMessage }, {
                "extensions", new JObject {
                    new JProperty("code", code)
                }
            }
        };
    }

    protected override async ValueTask VisitOperationDefinitionAsync(GraphQLOperationDefinition operationDefinition, IGraphQLContext context) {
        if(operationDefinition.Operation is OperationType.Query or OperationType.Mutation)
            context.IncreaseExpectedOperations(operationDefinition.SelectionSet.Selections.Count);

        var visitor = new OperationVisitor(_options, _dependencyResolver, _fragmentAccessor, _valueAccessor, operationDefinition.Operation, context);
        await visitor.VisitAsync(operationDefinition, context).ConfigureAwait(false);
    }
}