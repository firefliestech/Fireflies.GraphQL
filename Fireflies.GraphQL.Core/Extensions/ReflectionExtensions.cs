using System.Reflection;
using System.Runtime.CompilerServices;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Schema;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Extensions;

public static class ReflectionExtensions {
    public static string GraphQLName(this MemberInfo member) {
        if(member is TypeInfo { IsInterface: true } && member.Name.Length > 1 && member.Name[0] == 'I' && char.IsUpper(member.Name[1])) {
            return LowerCaseGraphQLName(member.Name.Substring(1));
        }

        return LowerCaseGraphQLName(member.Name);
    }

    public static string GraphQLName(this ParameterInfo parameter) {
        return LowerCaseGraphQLName(parameter.Name!);
    }

    private static string LowerCaseGraphQLName(string name) {
        if(name.StartsWith("__"))
            return $"__{char.ToLower(name[2])}{name[3..]}";

        return name.LowerCaseFirstLetter();
    }

    public static string LowerCaseFirstLetter(this string name) {
        if(char.IsUpper(name[0]))
            return name.Length == 1 ? $"{char.ToLower(name[0])}" : $"{char.ToLower(name[0])}{name[1..]}";

        return name;
    }

    public static string GetPrimitiveGraphQLName(this Type type) {
        if(type == typeof(int))
            return "Int";
        if(type == typeof(string))
            return "String";
        if(type == typeof(bool))
            return "Boolean";
        if(type == typeof(decimal))
            return "Float";
        if(type.IsSubclassOf(typeof(GraphQLId)))
            return "ID";

        return type.Name;
    }

    public static Type GetGraphQLType(this Type type) {
        if(type.IsTaskOrAsyncEnumerable(out var genericType))
            type = genericType!;

        if(type.IsCollection(out var elementType))
            type = elementType;

        return type;
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
        return ReflectionCache.GetParameters(methodInfo).Where(x =>
            !x.HasCustomAttribute<ResolvedAttribute>() &&
            !x.HasCustomAttribute<EnumeratorCancellationAttribute>() &&
            !x.ParameterType.IsAssignableTo(typeof(ASTNode)) &&
            !x.ParameterType.IsAssignableTo(typeof(IConnectionContext)) &&
            !x.ParameterType.IsAssignableTo(typeof(RequestContext))
        );
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

    public static Type GetGraphQLBaseType(this Type type) {
        type = type.GetGraphQLType();
        type = Nullable.GetUnderlyingType(type) ?? type;

        if(type.IsEnum)
            return type;

        switch(Type.GetTypeCode(type)) {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Byte:
            case TypeCode.SByte:
                return typeof(int);

            case TypeCode.Boolean:
                return typeof(bool);

            case TypeCode.Char:
            case TypeCode.String:
                return typeof(string);

            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return typeof(decimal);

            default:
                return type;
        }
    }
}