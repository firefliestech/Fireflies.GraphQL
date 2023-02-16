namespace Fireflies.GraphQL.Core.Extensions;

internal static class EnumerableExtensions {
    public static bool IsEnumerable(this Type type) {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>) || type.IsArray;
    }

    public static bool IsEnumerable(this Type type, out Type elementType) {
        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)) {
            elementType = type.GetGenericArguments()[0];
        } else {
            elementType = type;
        }

        var isEnumerable = elementType.IsEnumerable();
        if(isEnumerable)
            elementType = GetEnumerableType(elementType);

        return isEnumerable;
    }

    private static Type GetEnumerableType(this Type type) {
        return type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
    }
}