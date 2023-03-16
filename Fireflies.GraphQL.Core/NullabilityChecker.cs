using System.Reflection;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core;

internal static class NullabilityChecker {
    private static readonly NullabilityInfoContext NullabilityContext = new();
    private static Dictionary<object, bool> _cache = new();

    public static bool IsNullable(ParameterInfo parameterInfo) {
        if(parameterInfo.HasCustomAttribute<GraphQLNullable>())
            return true;

        if(!_cache.TryGetValue(parameterInfo, out var value)) {
            lock(_cache) {
                value = NullabilityContext.Create(parameterInfo).ReadState == NullabilityState.Nullable;
                _cache[parameterInfo] = value;
            }
        }

        return value;
    }

    public static bool IsNullable(PropertyInfo propertyInfo) {
        if (propertyInfo.HasCustomAttribute<GraphQLNullable>())
            return true;

        if(!_cache.TryGetValue(propertyInfo, out var value)) {
            lock(_cache) {
                value = NullabilityContext.Create(propertyInfo).ReadState == NullabilityState.Nullable;
                _cache[propertyInfo] = value;
            }
        }

        return value;
    }
}