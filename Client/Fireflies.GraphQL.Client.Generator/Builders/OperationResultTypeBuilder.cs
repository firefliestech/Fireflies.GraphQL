using System.Text;
using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class OperationResultTypeBuilder : ResultTypeBuilderBase, ITypeBuilder {
    private readonly string _typeName;
    private readonly GraphQLOperationDefinition _operationDefinition;
    private readonly SchemaType _schemaType;
    private readonly GraphQLGeneratorContext _context;
    private readonly StringBuilder _stringBuilder = new();

    public OperationResultTypeBuilder(string typeName, GraphQLOperationDefinition operationDefinition, SchemaType? parentType, SchemaType schemaType, GraphQLGeneratorContext context) : base(typeName + "Result", operationDefinition, parentType, schemaType, context) {
        _typeName = typeName;
        _operationDefinition = operationDefinition;
        _schemaType = schemaType;
        _context = context;
    }

    public string Source() {
        return _stringBuilder.ToString();
    }

    public async Task Build() {
        TypeBuilder.AddOperationProperties();
        
        var dataName = $"{_typeName}Data";
        TypeBuilder.AddInterfaceImplementation($"IOperationResult<I{dataName}>");
        TypeBuilder.AddProperty(dataName, "Data", new SchemaField { Type = _schemaType, Name = "data" }, new FieldMatch { FoundOnType = new SchemaType() });

        var dataTypeBuilder = new SubResultTypeBuilder(dataName, _operationDefinition, null, _schemaType, _context);
        await dataTypeBuilder.Build();

        await TypeBuilder.Build();
    }
}