namespace Fireflies.GraphQL.Client.Generator.Schema;

public static class SchemaHelper {
    public static bool IsEnumerable(this SchemaField schemaField) {
        return schemaField.Type.Kind == SchemaTypeKind.LIST || (schemaField.Type.Kind == SchemaTypeKind.NON_NULL && schemaField.Type.OfType!.Kind == SchemaTypeKind.LIST);
    }

    public static bool IsNullable(this SchemaField schemaField) {
        return schemaField.Type.Kind != SchemaTypeKind.NON_NULL && (schemaField.Type.Kind == SchemaTypeKind.NON_NULL && schemaField.Type.OfType!.Kind != SchemaTypeKind.NON_NULL);
    }

    public static SchemaType GetOfType(this SchemaType fieldType, Dictionary<string, SchemaType> schemaTypes) {
        return fieldType.Kind switch {
            SchemaTypeKind.LIST => fieldType.OfType.GetOfType(schemaTypes),
            SchemaTypeKind.NON_NULL => fieldType.OfType.GetOfType(schemaTypes),
            _ => schemaTypes[fieldType.Name]
        };
    }

    public static string GetNetType(this SchemaInputValue field) {
        return InternalGetDotnetType(field.Type);
    }

    public static string GetNetType(this SchemaField field) {
        return InternalGetDotnetType(field.Type);
    }

    public static string GetNetType(this SchemaField field, string typeName) {
        return InternalGetDotnetType(field.Type, typeName);
    }

    public static string GetDotnetType(this SchemaType fieldType) {
        return InternalGetDotnetType(fieldType);
    }

    internal static string InternalGetDotnetType(SchemaType fieldType, bool nullable = true) {
        return fieldType.Kind switch {
            SchemaTypeKind.SCALAR => TypeMapper.FromGraphQL(fieldType.Name) + (nullable ? "?" : ""),
            SchemaTypeKind.LIST => "IEnumerable<" + InternalGetDotnetType(fieldType.OfType, nullable) + ">",
            SchemaTypeKind.NON_NULL => InternalGetDotnetType(fieldType.OfType, false),
            SchemaTypeKind.OBJECT => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.INTERFACE => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.UNION => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.ENUM => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.INPUT_OBJECT => fieldType.Name + (nullable ? "?" : ""),
            _ => fieldType.Name + (nullable ? "?" : "")
        };
    }

    public static string GetDotnetType(this SchemaType fieldType, string name) {
        return InternalGetDotnetType(fieldType, name);
    }

    private static string InternalGetDotnetType(SchemaType fieldType, string name, bool nullable = true) {
        switch(fieldType.Kind) {
            case SchemaTypeKind.SCALAR:
                return TypeMapper.FromGraphQL(name) + (nullable ? "?" : "");
            case SchemaTypeKind.LIST:
                return "IEnumerable<" + InternalGetDotnetType(fieldType.OfType, name, nullable) + ">";
            case SchemaTypeKind.NON_NULL:
                return InternalGetDotnetType(fieldType.OfType, name, false);

            case SchemaTypeKind.OBJECT:
            case SchemaTypeKind.INTERFACE:
            case SchemaTypeKind.UNION:
            case SchemaTypeKind.ENUM:
            case SchemaTypeKind.INPUT_OBJECT:
            default:
                return name + (nullable ? "?" : "");
        }
    }

    public static SchemaType GetOfType(this SchemaField schemaType, GraphQLGeneratorContext context) {
        return schemaType.Type.GetOfType(context);
    }

    public static SchemaType GetOfType(this SchemaType schemaType, GraphQLGeneratorContext context) {
        return schemaType.Kind switch {
            SchemaTypeKind.LIST => schemaType.OfType.GetOfType(context),
            SchemaTypeKind.NON_NULL => schemaType.OfType.GetOfType(context),
            _ => context.GetSchemaType(schemaType.Name)
        };
    }

    public static SchemaType GetOfType(this SchemaField schemaType, GraphQLRootGeneratorContext context) {
        return schemaType.Type.GetOfType(context);
    }

    public static SchemaType GetOfType(this SchemaType schemaType, GraphQLRootGeneratorContext context) {
        return schemaType.Kind switch {
            SchemaTypeKind.LIST => schemaType.OfType.GetOfType(context),
            SchemaTypeKind.NON_NULL => schemaType.OfType.GetOfType(context),
            _ => context.GetSchemaType(schemaType.Name)
        };
    }
}