using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace Fireflies.GraphQL.Client.Generator;

public class GraphQLGeneratorContext : IASTVisitorContext {
    public GraphQLRootGeneratorContext RootContext { get; }

    public GraphQLDocument Document { get; }
    public FragmentAccessor FragmentAccessor { get; }

    public CancellationToken CancellationToken => RootContext.CancellationToken;

    public GraphQLGeneratorContext(GraphQLRootGeneratorContext rootContext, GraphQLDocument document, FragmentAccessor fragmentAccessor) {
        RootContext = rootContext;
        Document = document;
        FragmentAccessor = fragmentAccessor;
    }

    public SchemaType GetSchemaType(string type) {
        return RootContext.SchemaTypes[type];
    }

    public SchemaType GetSchemaType(GraphQLNamedType namedType) {
        return GetSchemaType(namedType.Name.StringValue);
    }
}