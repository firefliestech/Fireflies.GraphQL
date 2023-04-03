using System.Collections.Concurrent;
using Fireflies.Utility.Reflection;

namespace Fireflies.GraphQL.Core.Extensions;

public static class EnumerableExtensions {
    private static readonly ConcurrentDictionary<Type, Type?> _collectionCache = new();

    public static bool IsQueryable(this Type type) {
        return type.IsQueryable(out _);
    }

    public static bool IsCollection(this Type type, out Type? elementType) {
        var realType = type.DiscardTask();
        elementType = realType;

        var cachedType = _collectionCache.GetOrAdd(type, _ => {
            if(realType.IsEnumerable(out var enumerableType) || realType.IsQueryable(out enumerableType))
                return enumerableType;

            var implementICollection = realType.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));
            return implementICollection?.GetGenericArguments()[0];
        });

        if(cachedType == null)
            return false;

        elementType = cachedType;
        return true;
    }

    public static bool IsCollection(this Type type) {
        return type.IsCollection(out _);
    }

    private static bool IsQueryable(this Type type, out Type elementType) {
        return GetElementTypeForEnumerableOf(type, out elementType, typeof(IQueryable<>), false);
    }

    private static bool IsEnumerable(this Type type, out Type elementType) {
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

    private static Type GetElementType(this Type type) {
        return type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
    }
}