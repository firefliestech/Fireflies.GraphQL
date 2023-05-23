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

    public static string GetNetType(this SchemaField field) {
        return InternalGetDotnetType(field.Type, false, true);
    }

    public static string GetNetType(this SchemaType type, bool skipList = false) {
        return InternalGetDotnetType(type, skipList, true);
    }

    internal static string InternalGetDotnetType(SchemaType fieldType, bool skipList, bool nullable) {
        return fieldType.Kind switch {
            SchemaTypeKind.SCALAR => TypeMapper.FromGraphQL(fieldType.Name) + (nullable ? "?" : ""),
            SchemaTypeKind.LIST => skipList ? InternalGetDotnetType(fieldType.OfType, skipList, nullable) : "IEnumerable<" + InternalGetDotnetType(fieldType.OfType, skipList, nullable) + ">",
            SchemaTypeKind.NON_NULL => InternalGetDotnetType(fieldType.OfType, skipList, false),
            SchemaTypeKind.OBJECT => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.INTERFACE => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.UNION => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.ENUM => fieldType.Name + (nullable ? "?" : ""),
            SchemaTypeKind.INPUT_OBJECT => fieldType.Name + (nullable ? "?" : ""),
            _ => fieldType.Name + (nullable ? "?" : "")
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