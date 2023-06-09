using System.Collections.Concurrent;
using System.Reflection;
using Fireflies.GraphQL.Abstractions.Authorization;
using Fireflies.GraphQL.Core.Exceptions;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core;

internal static class AuthorizationHelper {
    private static readonly ConcurrentDictionary<Type, MethodInfo> MethodCache = new();

    public static async Task Authorize(MemberInfo memberInfo, GraphQLField field, IRequestContext requestContext) {
        var authorizationAttributes = memberInfo.CustomAttributes.Select(x => x.AttributeType).Where(c => c.IsAssignableTo(typeof(GraphQLAuthorizationBaseAttribute)));
        await Authorize(requestContext, field, authorizationAttributes).ConfigureAwait(false);
    }

    private static async Task Authorize(IRequestContext requestContext, GraphQLField field, IEnumerable<Type> authorizationAttributes) {
        var any = false;

        foreach(var authorizationAttribute in authorizationAttributes) {
            any = true;
            var handler = requestContext.DependencyResolver.Resolve(authorizationAttribute);

            var actualAuthorizeMethod = MethodCache.GetOrAdd(authorizationAttribute, _ => authorizationAttribute.GetMethod(nameof(GraphQLAuthorizationAttribute.Authorize), BindingFlags.Instance | BindingFlags.Public)!);
            var argumentBuilder = new ArgumentBuilder(field.Arguments, actualAuthorizeMethod, requestContext, null);
            var arguments = await argumentBuilder.Build(field);
            var authorized = await ((Task<bool>)actualAuthorizeMethod.Invoke(handler, arguments)!).ConfigureAwait(false);
            if(authorized)
                return;
        }

        if(!any)
            return;

        throw new GraphQLUnauthorizedException();
    }
}