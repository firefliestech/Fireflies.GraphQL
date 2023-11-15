using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Fireflies.Utility.Reflection;
using Fireflies.Utility.Reflection.Fasterflect;
using MethodInvoker = Fireflies.Utility.Reflection.Fasterflect.MethodInvoker;

namespace Fireflies.GraphQL.Core;

public static class ReflectionCache {
    private static readonly ConcurrentDictionary<Type, Type[]> _typeImplementationsCache = new();
    private static readonly ConcurrentDictionary<Type, MemberCache> _memberCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, Type> _taskDiscardedReturnTypeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, ParameterInfo[]> _methodParameterCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, Type[]> _methodParameterTypeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, MethodInvoker> _executeMethodCache = new();

    public static MethodInfo InternalExecuteMethodInfo { get; set; }

    static ReflectionCache() {
        InternalExecuteMethodInfo = typeof(ReflectionCache).GetMethod(nameof(InternalExecuteMethod), BindingFlags.Static | BindingFlags.NonPublic)!;
    }

    public static IEnumerable<Type> GetAllClassesThatImplements(Type baseType, bool includeDynamic = true) {
        return _typeImplementationsCache.GetOrAdd(baseType, _ =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(x => includeDynamic || !x.IsDynamic)
                .SelectMany(x => x.GetExportedTypes())
                .Where(x => x.IsClass && x.IsAssignableTo(baseType) && !x.IsAbstract)
                .ToArray());
    }

    public static IEnumerable<MemberInfo> GetMembers(Type type) {
        return _memberCache.GetOrAdd(type, _ => new MemberCache(type));
    }

    public static MemberInfo? GetMemberCache(Type type, string memberName) {
        var memberCache = _memberCache.GetOrAdd(type, _ => new MemberCache(type));
        return memberCache.GetMember(memberName);
    }

    public static ParameterInfo[] GetParameters(MethodInfo methodInfo) {
        return _methodParameterCache.GetOrAdd(methodInfo, _ => methodInfo.GetParameters());
    }

    public static Type[] GetParameterTypes(MethodInfo methodInfo) {
        return _methodParameterTypeCache.GetOrAdd(methodInfo, _ => methodInfo.GetParameters().Select(x => x.ParameterType).ToArray());
    }

    public static Type GetReturnType(MethodInfo methodInfo) {
        return _taskDiscardedReturnTypeCache.GetOrAdd(methodInfo, _ => methodInfo.ReturnType.DiscardTask());
    }

    public static MethodInvoker GetGenericMethodInvoker(MethodInfo methodInfo, Type[] genericArguments, params Type[] parameterTypes) {
        return Reflect.Method(methodInfo, genericArguments, parameterTypes);
    }

    public static Task<object?> ExecuteMethod(MethodInfo methodInfo, object instance, object?[] arguments) {
        var invoker = _executeMethodCache.GetOrAdd(methodInfo, _ => {
            var returnType = GetReturnType(methodInfo);
            return Reflect.Method(InternalExecuteMethodInfo, new[] { returnType }, typeof(MethodInfo), typeof(object), typeof(object[]));
        });

        return (Task<object?>)invoker(null, methodInfo, instance, arguments);
    }

    private static async Task<object?> InternalExecuteMethod<T>(MethodInfo methodInfo, object instance, object[] arguments) {
        if(!methodInfo.ReturnType.IsTask())
            return InternalInvoke(methodInfo, instance, arguments);

        var invokeResult = (Task<T>)InternalInvoke(methodInfo, instance, arguments)!;
        return await invokeResult.ConfigureAwait(false);
    }

    private static object? InternalInvoke(MethodInfo methodInfo, object instance, object[] arguments) {
        var methodInvoker = Reflect.Method(methodInfo, GetParameterTypes(methodInfo));
        return methodInvoker.Invoke(instance, arguments);
    }

    private class MemberCache : IEnumerable<MemberInfo> {
        private readonly Dictionary<string, MemberInfo> _cache = new();

        public MemberCache(Type type) {
            foreach(var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase).Where(p => p.DeclaringType != typeof(object) && p is MethodInfo or PropertyInfo)) {
                if(!_cache.TryAdd(member.Name.ToLower(), member)) {
                    throw new Exception("Duplicate member name");
                }
            }
        }

        public MemberInfo? GetMember(string memberName) {
            if(_cache.TryGetValue(memberName.ToLower(), out var value))
                return value;

            return null;
        }

        public IEnumerator<MemberInfo> GetEnumerator() {
            return _cache.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}