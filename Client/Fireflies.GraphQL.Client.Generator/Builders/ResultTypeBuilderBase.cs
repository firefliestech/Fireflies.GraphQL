using Fireflies.GraphQL.Client.Generator.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public abstract class ResultTypeBuilderBase {
    private readonly string _typeName;
    private readonly ASTNode _astNode;
    private readonly SchemaType? _parentType;
    private readonly SchemaType _schemaType;

    protected readonly GraphQLGeneratorContext Context;
    protected readonly TypeBuilder TypeBuilder;
    private bool _exactTypeConditionRequired;

    protected ResultTypeBuilderBase(string typeName, ASTNode astNode, SchemaType? parentType, SchemaType schemaType, GraphQLGeneratorContext context) {
        _typeName = typeName;
        _astNode = astNode;
        _parentType = parentType;
        _schemaType = schemaType;
        Context = context;
        TypeBuilder = Context.RootContext.GetTypeBuilder(typeName, astNode, context);
    }

    public async Task GenerateProperties() {
        foreach(var field in _schemaType.Fields) {
            var fieldType = field.Type.GetOfType(Context);
            var match = await field.IsSelected(_schemaType, _parentType, _astNode, _exactTypeConditionRequired, Context);
            if(!match.IsSelected)
                continue;

            var propertyName = field.Name.Capitalize();

            if(fieldType.Kind is SchemaTypeKind.ENUM or SchemaTypeKind.SCALAR) {
                if(match.DefinedByFragment != null && match.DefinedByFragment != _astNode) {
                    var fragmentBuilder = new FragmentTypeBuilder(match.DefinedByFragment, Context);
                    await fragmentBuilder.Build();
                }

                TypeBuilder.AddProperty($"{field.GetNetType(skipList: true, skipNullable: true)}", propertyName, field, match);
            } else {
                await AddObjectProperty(propertyName, fieldType, match, field);
            }
        }
    }

    public void ExactTypeConditionRequired() {
        _exactTypeConditionRequired = true;
    }

    private async Task AddObjectProperty(string propertyName, SchemaType fieldType, FieldMatch fieldMatch, SchemaField field) {
        var subClassName = $"{_typeName}_{propertyName}";
        var subInterfaceName = $"I{subClassName}";

        if(fieldMatch.DefinedByFragment != null && fieldMatch.DefinedByFragment != _astNode) {
            var fragmentBuilder = new FragmentTypeBuilder(fieldMatch.DefinedByFragment, Context);
            var fragmentClassName = await fragmentBuilder.Build();

            if(fieldMatch.ConditionType != null)
                fragmentClassName = $"{fragmentClassName}_{fieldMatch.ConditionType!.Name!.Capitalize()}";
            fragmentClassName = $"{fragmentClassName}_{propertyName}";

            if(fieldType.Kind is SchemaTypeKind.INTERFACE or SchemaTypeKind.UNION) {
                foreach(var possibleType in field.Type.GetOfType(Context).PossibleTypes.Select(x => x.GetOfType(Context))) {
                    TypeBuilder.AddPolymorphicProperty(typeName: $"I{fragmentClassName}",
                        propertyName: propertyName,
                        className: $"{fragmentClassName}_{possibleType.Name.Capitalize()}",
                        interfaceName: $"I{fragmentClassName}",
                        schemaField: field,
                        schemaType: possibleType,
                        fieldMatch);
                }
            } else {
                TypeBuilder.AddProperty(fragmentClassName, propertyName, field, fieldMatch);
            }
        } else if(fieldType.Kind is SchemaTypeKind.INTERFACE or SchemaTypeKind.UNION) {
            var subInterfaceBuilder = new SubResultTypeBuilder(subClassName, fieldMatch.SelectedByNode, null, fieldType, Context);
            subInterfaceBuilder.OnlyInterface();
            await subInterfaceBuilder.Build();

            foreach(var possibleType in field.Type.GetOfType(Context).PossibleTypes.Select(x => x.GetOfType(Context))) {
                var className = $"{subClassName}_{possibleType.Name.Capitalize()}";
                var possibleTypeBuilder = new SubResultTypeBuilder(className, fieldMatch.SelectedByNode, fieldType, possibleType, Context);
                possibleTypeBuilder.AddInterfaceImplementation(subInterfaceName);
                await possibleTypeBuilder.Build();

                TypeBuilder.AddPolymorphicProperty(typeName: $"I{subClassName}",
                    propertyName: propertyName,
                    className: className,
                    interfaceName: $"I{className}",
                    schemaField: field,
                    schemaType: possibleType,
                    fieldMatch);
            }
        } else {
            var subResultTypeBuilder = new SubResultTypeBuilder(subClassName, fieldMatch.SelectedByNode, null, fieldType, Context);
            await subResultTypeBuilder.Build();
            TypeBuilder.AddProperty(subClassName, propertyName, field, fieldMatch);
        }
    }
}