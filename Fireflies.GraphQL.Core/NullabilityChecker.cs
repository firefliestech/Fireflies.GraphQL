using System.Reflection;

namespace Fireflies.GraphQL.Core;

internal static class NullabilityChecker {
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public static bool IsNullable(ParameterInfo parameterInfo) {
        return NullabilityContext.Create(parameterInfo).ReadState == NullabilityState.Nullable;
    }

    public static bool IsNullable(PropertyInfo propertyInfo) {
        return NullabilityContext.Create(propertyInfo).ReadState == NullabilityState.Nullable;
    }
}