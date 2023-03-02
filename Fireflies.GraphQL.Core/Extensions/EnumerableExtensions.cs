namespace Fireflies.GraphQL.Core.Extensions;

public static class EnumerableExtensions {
    public static bool IsQueryable(this Type type, out Type elementType) {
        return GetElementTypeForEnumerableOf(type, out elementType, typeof(IQueryable<>), false);
    }

    public static bool IsQueryable(this Type type) {
        return type.IsQueryable(out _);
    }

    public static bool IsEnumerable(this Type type, out Type elementType) {
        return GetElementTypeForEnumerableOf(type, out elementType, typeof(IEnumerable<>), true);
    }

    private static bool GetElementTypeForEnumerableOf(Type type, out Type elementType, Type lookingFor, bool checkArray) {
        elementType = type.DiscardTask();

        if(elementType.IsGenericType) {
            var typeDefinition = elementType.GetGenericTypeDefinition();

            if(typeDefinition == lookingFor) {
                elementType = GetElementType(elementType);
                return true;
            }
        }

        if(checkArray && elementType.IsArray) {
            elementType = GetElementType(elementType);
            return true;
        }

        return false;
    }

    public static bool IsEnumerable(this Type type) {
        return type.IsEnumerable(out _);
    }

    public static bool IsCollection(this Type type, out Type elementType) {
        elementType = type.DiscardTask();

        if(type.IsEnumerable(out var enumerableType) || elementType.IsQueryable(out enumerableType)) {
            elementType = enumerableType;
            return true;
        }

        var implementICollection = type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));

        if(implementICollection != null) {
            elementType = implementICollection.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    public static bool IsCollection(this Type type) {
        return type.IsCollection(out _);
    }

    private static Type GetElementType(this Type type) {
        return type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
    }
}