using System.Collections.ObjectModel;
using System.Reflection;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.Utility.Reflection;

namespace Fireflies.GraphQL.Core;

internal static class NullabilityChecker {
    private static readonly NullabilityInfoContext NullabilityContext = new();
    private static readonly Dictionary<object, bool> _cache = new();

    public static bool IsNullable(ParameterInfo parameterInfo) {
        if(!_cache.TryGetValue(parameterInfo, out var value)) {
            lock(_cache) {
                if(parameterInfo.HasCustomAttribute<GraphQLNullable>()) {
                    value = true;
                } else {
                    var nullability = NullabilityContext.Create(parameterInfo);
                    if(parameterInfo.ParameterType.IsTask()) {
                        value = nullability.GenericTypeArguments[0].ReadState == NullabilityState.Nullable;
                    } else {
                        value = nullability.ReadState == NullabilityState.Nullable;
                    }
                }
                _cache[parameterInfo] = value;
            }
        }

        return value;
    }

    public static bool IsNullable(PropertyInfo propertyInfo) {
        if(!_cache.TryGetValue(propertyInfo, out var value)) {
            lock(_cache) {
                value = propertyInfo.HasCustomAttribute<GraphQLNullable>() || NullabilityContext.Create(propertyInfo).ReadState == NullabilityState.Nullable;
                _cache[propertyInfo] = value;
            }
        }

        return value;
    }

    public static bool IsNullable(MethodInfo methodInfo) {
        if(_cache.TryGetValue(methodInfo, out var value))
            return value;

        lock(_cache) {
            value = methodInfo.HasCustomAttribute<GraphQLNullable>() || IsNullable(methodInfo.ReturnParameter);
            _cache[methodInfo] = value;
        }

        return value;
    }
}