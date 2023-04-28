using System.Text;
using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class OperationResultTypeBuilder : ResultTypeBuilderBase, ITypeBuilder {
    private readonly StringBuilder _stringBuilder = new();

    public OperationResultTypeBuilder(string typeName, GraphQLOperationDefinition operationDefinition, SchemaType? parentType, SchemaType schemaType, GraphQLGeneratorContext context) : base(typeName, operationDefinition, parentType, schemaType, context) {
    }

    public string Source() {
        return _stringBuilder.ToString();
    }

    public async Task Build() {
        TypeBuilder.AddOperationProperties();
        await GenerateProperties();
        
        await TypeBuilder.Build();
    }
}