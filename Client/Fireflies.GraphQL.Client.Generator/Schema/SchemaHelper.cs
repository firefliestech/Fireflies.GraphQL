namespace Fireflies.GraphQL.Client.Generator.Schema;

public static class SchemaHelper {
    public static bool IsEnumerable(this SchemaField schemaField) {
        return schemaField.Type.Kind == SchemaTypeKind.LIST || (schemaField.Type.Kind == SchemaTypeKind.NON_NULL && schemaField.Type.OfType!.Kind == SchemaTypeKind.LIST);
    }

    public static bool IsNullable(this SchemaField schemaField) {
        if(schemaField.Type.Kind == SchemaTypeKind.NON_NULL)
            return false;

        return true;
    }

    public static SchemaType GetOfType(this SchemaType fieldType, Dictionary<string, SchemaType> schemaTypes) {
        return fieldType.Kind switch {
            SchemaTypeKind.LIST => fieldType.OfType.GetOfType(schemaTypes),
            SchemaTypeKind.NON_NULL => fieldType.OfType.GetOfType(schemaTypes),
            _ => schemaTypes[fieldType.Name]
        };
    }

    public static string GetNetType(this SchemaType schemaType) {
        return InternalGetDotnetType(schemaType, null, false, true);
    }

    public static string GetNetType(this SchemaField field, bool skipList = false, bool skipNullable = false) {
        return InternalGetDotnetType(field.Type, null, skipList, !skipNullable);
    }

    internal static string InternalGetDotnetType(SchemaType fieldType, string? overrideName, bool skipList, bool nullable) {
        return fieldType.Kind switch {
            SchemaTypeKind.SCALAR => TypeMapper.FromGraphQL(fieldType.Name) + (nullable ? "?" : ""),
            SchemaTypeKind.LIST => skipList ? InternalGetDotnetType(fieldType.OfType, overrideName, skipList, nullable) : "IEnumerable<" + InternalGetDotnetType(fieldType.OfType, overrideName, skipList, nullable) + ">",
            SchemaTypeKind.NON_NULL => InternalGetDotnetType(fieldType.OfType, overrideName, skipList, false),
            SchemaTypeKind.OBJECT => (overrideName ?? fieldType.Name) + (nullable ? "?" : ""),
            SchemaTypeKind.INTERFACE => (overrideName ?? fieldType.Name) + (nullable ? "?" : ""),
            SchemaTypeKind.UNION => (overrideName ?? fieldType.Name) + (nullable ? "?" : ""),
            SchemaTypeKind.ENUM => (overrideName ?? fieldType.Name) + (nullable ? "?" : ""),
            SchemaTypeKind.INPUT_OBJECT => (overrideName ?? fieldType.Name) + (nullable ? "?" : ""),
            _ => (overrideName ?? fieldType.Name) + (nullable ? "?" : "")
        };
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

    public static SchemaType GetOfType(this SchemaType schemaType, GraphQLRootGeneratorContext context) {
        return schemaType.Kind switch {
            SchemaTypeKind.LIST => schemaType.OfType.GetOfType(context),
            SchemaTypeKind.NON_NULL => schemaType.OfType.GetOfType(context),
            _ => context.GetSchemaType(schemaType.Name)
        };
    }
}