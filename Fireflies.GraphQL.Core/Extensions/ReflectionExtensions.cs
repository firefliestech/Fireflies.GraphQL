using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Abstractions;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Extensions;

public static class ReflectionExtensions {
    private static readonly MethodInfo InternalExecuteMethodInfo;
    private static readonly ConcurrentDictionary<Type, Type[]> TypeImplementationsCache = new();

    static ReflectionExtensions() {
        InternalExecuteMethodInfo = typeof(ReflectionExtensions).GetMethod(nameof(InternalExecuteMethod), BindingFlags.Static | BindingFlags.NonPublic)!;
    }

    public static string GraphQLName(this MemberInfo member) {
        if(member is TypeInfo { IsInterface: true } && member.Name.Length > 1 && member.Name[0] == 'I' && char.IsUpper(member.Name[1])) {
            return LowerCaseGraqhQLName(member.Name.Substring(1));
        }

        return LowerCaseGraqhQLName(member.Name);
    }

    public static string GraphQLName(this ParameterInfo parameter) {
        return LowerCaseGraqhQLName(parameter.Name!);
    }

    private static string LowerCaseGraqhQLName(string name) {
        if(name.StartsWith("__"))
            return $"__{char.ToLower(name[2])}{name[3..]}";

        return name.LowerCaseFirstLetter();
    }

    public static string LowerCaseFirstLetter(this string name) {
        if(char.IsUpper(name[0]))
            return name.Length == 1 ? $"{char.ToLower(name[0])}" : $"{char.ToLower(name[0])}{name[1..]}";

        return name;
    }

    public static bool HasCustomAttribute<T>(this MemberInfo memberInfo) where T : Attribute {
        return memberInfo.HasCustomAttribute<T>(out _);
    }

    public static bool HasCustomAttribute<T>(this MemberInfo memberInfo, out T? attribute) where T : Attribute {
        var attributes = memberInfo.GetCustomAttributes<T>(true);
        attribute = attributes.FirstOrDefault();
        return attribute != null;
    }

    public static bool HasCustomAttribute<T>(this ParameterInfo parameterInfo) where T : Attribute {
        return parameterInfo.HasCustomAttribute<T>(out _);
    }

    public static bool HasCustomAttribute<T>(this ParameterInfo parameterInfo, out T? attribute) where T : Attribute {
        var attributes = parameterInfo.GetCustomAttributes<T>(true);
        attribute = attributes.FirstOrDefault();
        return attribute != null;
    }

    public static Type GetGraphQLType(this Type type) {
        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            type = type.GetGenericArguments()[0];

        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            type = type.GetGenericArguments()[0];

        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            type = type.GetGenericArguments()[0];

        if(type.IsCollection(out var elementType))
            type = elementType;

        return type;
    }

    public static Type ElementType(this Type type) {
        type.IsCollection(out var elementType);
        return elementType;
    }

    public static Type DiscardTaskFromReturnType(this MethodInfo methodInfo) {
        return methodInfo.ReturnType.DiscardTask();
    }

    public static Type DiscardTask(this Type type) {
        if(type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)))
            return type.GetGenericArguments()[0];

        return type;
    }

    public static bool IsValidGraphQLObject(this Type type) {
        return Type.GetTypeCode(type) == TypeCode.Object
               && type != typeof(DateTimeOffset)
               && type != typeof(DateTimeOffset?);
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

    public static IEnumerable<ParameterInfo> GetAllGraphQLParameters(this MethodInfo methodInfo) {
        return methodInfo.GetParameters().Where(x =>
            !x.HasCustomAttribute<ResolvedAttribute>() &&
            !x.HasCustomAttribute<EnumeratorCancellationAttribute>() &&
            !x.ParameterType.IsAssignableTo(typeof(ASTNode)) &&
            !x.ParameterType.IsAssignableTo(typeof(IGraphQLContext))
        );
    }

    public static async Task<object?> ExecuteMethod(this MethodInfo methodInfo, object instance, object?[] arguments) {
        var isEnumerable = methodInfo.ReturnType.IsCollection(out var elementType);
        var result = (Task<object?>)InternalExecuteMethodInfo.MakeGenericMethod(methodInfo.DiscardTaskFromReturnType()).Invoke(null, new[] { methodInfo, instance, isEnumerable, arguments })!;
        return await result.ConfigureAwait(false);
    }

    private static async Task<object?> InternalExecuteMethod<T>(MethodInfo methodInfo, object instance, bool isEnumerable, object[] arguments) {
        if(methodInfo.ReturnType.IsGenericType) {
            var genericTypeDefinition = methodInfo.ReturnType.GetGenericTypeDefinition();
            if(genericTypeDefinition == typeof(Task<>) || genericTypeDefinition == typeof(ValueTask<>)) {
                var invokeResult = (Task<T>)methodInfo.Invoke(instance, arguments)!;
                return await invokeResult.ConfigureAwait(false);
            }
        }

        return methodInfo.Invoke(instance, arguments);
    }

    public static IEnumerable<Type> GetAllClassesThatImplements(this Type baseType) {
        return TypeImplementationsCache.GetOrAdd(baseType, _ =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => x.IsClass && x.IsAssignableTo(baseType))
                .ToArray());
    }

    public static string? GetDescription(this MemberInfo memberInfo) {
        if(memberInfo.HasCustomAttribute<GraphQLDescriptionAttribute>(out var attribute))
            return attribute!.Description;

        return null;
    }

    public static string? GetDescription(this ParameterInfo parameterInfo) {
        if(parameterInfo.HasCustomAttribute<GraphQLDescriptionAttribute>(out var attribute))
            return attribute!.Description;

        return null;
    }

    public static string? GetDeprecatedReason(this MemberInfo memberInfo) {
        if(memberInfo.HasCustomAttribute<GraphQLDeprecatedAttribute>(out var attribute))
            return attribute!.Reason;

        return null;
    }

    public static bool IsTask(this Type type, out Type? returnType) {
        returnType = type;

        if(type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>))) {
            returnType = type.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    public static bool IsTask(this Type type) {
        return IsTask(type, out _);
    }

    public static bool IsAsyncEnumerable(this Type type, out Type? returnType) {
        returnType = type;

        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)) {
            returnType = type.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    public static bool IsAsyncEnumerable(this Type type) {
        return IsAsyncEnumerable(type, out _);
    }
}