using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace Fireflies.GraphQL.Core.Extensions;

public static class QueryableExtensions {
    public static IOrderedQueryable<T> OrderByMember<T>(this IQueryable<T> source, string memberPath) {
        return source.OrderByMemberUsing(memberPath, "OrderBy");
    }

    public static IOrderedQueryable<T> OrderByMemberDescending<T>(this IQueryable<T> source, string memberPath) {
        return source.OrderByMemberUsing(memberPath, "OrderByDescending");
    }

    public static IOrderedQueryable<T> ThenByMember<T>(this IOrderedQueryable<T> source, string memberPath) {
        return source.OrderByMemberUsing(memberPath, "ThenBy");
    }

    public static IOrderedQueryable<T> ThenByMemberDescending<T>(this IOrderedQueryable<T> source, string memberPath) {
        return source.OrderByMemberUsing(memberPath, "ThenByDescending");
    }

    private static IOrderedQueryable<T> OrderByMemberUsing<T>(this IQueryable<T> source, string memberPath, string method) {
        var parameter = Expression.Parameter(typeof(T), "item");

        var memberParts = GetMemberParts<T>(memberPath);
        var member = memberParts.Aggregate((Expression)parameter, Expression.PropertyOrField);
        var keySelector = Expression.Lambda(member, parameter);
        var methodCall = Expression.Call(typeof(Queryable), method, new[] { parameter.Type, member.Type }, source.Expression, Expression.Quote(keySelector));
        return (IOrderedQueryable<T>)source.Provider.CreateQuery(methodCall);
    }

    private static IEnumerable<string> GetMemberParts<T>(string memberPath) {
        var memberParts = memberPath.Split('.');

        var currentType = typeof(T);
        for(var i = 0; i < memberParts.Length; i++) {
            var member = currentType.GetMember(memberParts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase).FirstOrDefault();
            if(member != null) {
                memberParts[i] = member.Name;
                currentType = member switch {
                    FieldInfo fieldInfo => fieldInfo.FieldType,
                    MethodInfo methodInfo => methodInfo.ReturnType,
                    PropertyInfo propertyInfo => propertyInfo.PropertyType,
                    _ => throw new ArgumentOutOfRangeException(nameof(member))
                };
            } else {
                break;
            }
        }

        return memberParts;
    }
}