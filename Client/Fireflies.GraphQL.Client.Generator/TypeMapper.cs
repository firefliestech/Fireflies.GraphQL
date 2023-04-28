﻿namespace Fireflies.GraphQL.Client.Generator;

public static class TypeMapper {
    public static string FromGraphQL(string name) {
        switch(name) {
            case "Int":
                return "int";
            case "Bool":
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