using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Authorization;
using Fireflies.GraphQL.Core.Exceptions;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.Core;

internal static class AuthorizationHelper {
    public static async Task Authorize(IDependencyResolver dependencyResolver, MemberInfo memberInfo) {
        var authorizationAttributes = memberInfo.CustomAttributes.Select(x => x.AttributeType).Where(c => c.IsAssignableTo(typeof(GraphQLAuthorizationAttribute)));
        await Authorize(dependencyResolver, authorizationAttributes).ConfigureAwait(false);
    }

    private static async Task Authorize(IDependencyResolver dependencyResolver, IEnumerable<Type> authorizationAttributes) {
        var any = false;

        foreach(var authorizationAttribute in authorizationAttributes) {
            any = true;
            var authorization = (GraphQLAuthorizationAttribute)dependencyResolver.Resolve(authorizationAttribute);
            if(await authorization.Authorize().ConfigureAwait(false)) {
                return;
            }
        }

        if(!any)
            return;

        throw new GraphQLUnauthorizedException();
    }
}