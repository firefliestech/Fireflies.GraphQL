using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core;

internal static class NullabilityChecker {
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public static bool IsNullable(ParameterInfo parameterInfo) {
        if(parameterInfo.HasCustomAttribute<GraphQLNullable>())
            return true;

        return NullabilityContext.Create(parameterInfo).ReadState == NullabilityState.Nullable;
    }

    public static bool IsNullable(PropertyInfo propertyInfo) {
        if (propertyInfo.HasCustomAttribute<GraphQLNullable>())
            return true;

        return NullabilityContext.Create(propertyInfo).ReadState == NullabilityState.Nullable;
    }
}