namespace Fireflies.GraphQL.Client.Generator;

public static class TypeMapper {
    public static string FromGraphQL(string name) {
        switch(name) {
            case "Int":
                return "GraphQLInt";
            case "Boolean":
                return "bool";
            case "ID":
            case "String":
                return "string";
            case "Float":
                return "decimal";
            default:
                return name;
        }
    }
}