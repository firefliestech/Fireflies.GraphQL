using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class SubResultTypeBuilder : ResultTypeBuilderBase {
    public SubResultTypeBuilder(string typeName, ASTNode astNode, SchemaType? parentType, SchemaType schemaType, GraphQLGeneratorContext context) : base(typeName, astNode, parentType, schemaType, context) {
    }

    public async Task Build() {
        await GenerateProperties();

        await TypeBuilder.Build();
    }

    public void AddInterfaceImplementation(string interf) {
        TypeBuilder.AddInterfaceImplementation(interf);
    }

    public void OnlyInterface() {
        TypeBuilder.OnlyInterface();
    }
}