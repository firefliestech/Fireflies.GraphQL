using System.Reflection;
using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Core.Extensions;

internal static class ReflectionExtensions {
    private static readonly MethodInfo _internalExecuteMethod;

    static ReflectionExtensions() {
        _internalExecuteMethod = typeof(ReflectionExtensions).GetMethod(nameof(InternalExecuteMethod), BindingFlags.Static | BindingFlags.NonPublic);
    }

    public static string GraphQLName(this MemberInfo member) {
        if(member is TypeInfo { IsInterface: true } && member.Name.Length > 1 && member.Name[0] == 'I' && char.IsUpper(member.Name[1])) {
            return LowerCaseFirstLetter(member.Name.Substring(1));
        }
        return LowerCaseFirstLetter(member.Name);
    }

    public static string GraphQLName(this ParameterInfo parameter) {
        return LowerCaseFirstLetter(parameter.Name);
    }

    private static string LowerCaseFirstLetter(string name) {
        if(name.StartsWith("__")) {
            return $"__{char.ToLower(name[2])}{name[3..]}";
        }

        if(char.IsUpper(name[0]))
            return name.Length == 1 ? $"{char.ToLower(name[0])}" : $"{char.ToLower(name[0])}{name[1..]}";

        return name;
    }

    public static bool HasCustomAttribute<T>(this MemberInfo memberInfo) where T : Attribute {
        return memberInfo.GetCustomAttributes<T>(true).Any();
    }

    public static Type GetGraphQLType(this Type type) {
        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            type = type.GetGenericArguments()[0];

        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            type = type.GetGenericArguments()[0];

        if(type.IsEnumerable(out var elementType))
            type = elementType;

        return type;
    }

    public static Type DiscardTaskFromReturnType(this MethodInfo methodInfo) {
        var returnType = methodInfo.ReturnType;

        if(returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>) || returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)))
            return returnType.GetGenericArguments()[0];

        return returnType;
    }

    public static IEnumerable<PropertyInfo> GetAllGraphQLProperties(this Type type) {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.GetProperty | BindingFlags.IgnoreCase).Where(p => p.DeclaringType != typeof(object));
    }

    public static PropertyInfo GetGraphQLProperty(this Type type, string name) {
        return GetAllGraphQLProperties(type).Single(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
    }

    public static IEnumerable<MethodInfo> GetAllGraphQLMethods(this Type type, bool requireAttribute = false) {
        return GetAllGraphQLQueryMethods(type, requireAttribute).Union(GetAllGraphQLSubscriptionMethods(type, requireAttribute)).Union(GetAllGraphQLMutationMethods(type, requireAttribute));
    }

    public static IEnumerable<MethodInfo> GetAllGraphQLQueryMethods(this Type type, bool requireAttribute = false) {
        var allMethods = GetAllAccessibleMethods(type).Where(p => p.ReturnType != typeof(void));
        return requireAttribute ? allMethods.Where(x => x.HasCustomAttribute<GraphQLQueryAttribute>()) : allMethods;
    }

    public static IEnumerable<MethodInfo> GetAllGraphQLSubscriptionMethods(this Type type, bool requireAttribute = false) {
        var allMethods = GetAllAccessibleMethods(type).Where(p => p.ReturnType.IsGenericType && p.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        return requireAttribute ? allMethods.Where(x => x.HasCustomAttribute<GraphQLSubscriptionAttribute>()) : allMethods;
    }

    public static IEnumerable<MethodInfo> GetAllGraphQLMutationMethods(this Type type, bool requireAttribute = false) {
        var allMethods = GetAllAccessibleMethods(type).Where(p => p.ReturnType != typeof(void));
        return requireAttribute ? allMethods.Where(x => x.HasCustomAttribute<GraphQLMutationAttribute>()) : allMethods;
    }

    public static MemberInfo? GetGraphQLMemberInfo(this Type type, string name) {
        return GetAllGraphQLMemberInfo(type).SingleOrDefault(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
    }

    public static IEnumerable<MemberInfo> GetAllGraphQLMemberInfo(this Type type) {
        var methods = GetAllGraphQLQueryMethods(type);
        var properties = GetAllGraphQLProperties(type);
        return methods.Cast<MemberInfo>().Union(properties);
    }

    private static IEnumerable<MethodInfo> GetAllAccessibleMethods(Type type) {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).Where(p => p.DeclaringType != typeof(object) && !p.IsSpecialName);
    }

    public static async Task<object?> ExecuteMethod(this MethodInfo methodInfo, object instance, object?[] arguments) {
        var returnType = methodInfo.ReturnType;
        if(returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            returnType = returnType.GetGenericArguments()[0];
        var result = await (Task<object?>)_internalExecuteMethod.MakeGenericMethod(returnType).Invoke(null, new[] { methodInfo, instance, arguments })!;
        return result;
    }

    private static async Task<object?> InternalExecuteMethod<T>(MethodInfo methodInfo, object instance, object[] arguments) {
        var returnType = methodInfo.ReturnType;
        var invokeResult = methodInfo.Invoke(instance, arguments);
        if(returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)) {
            var result = await (Task<T>)invokeResult!;
            return result;
        }

        return invokeResult;
    }
}