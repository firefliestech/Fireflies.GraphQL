using Fireflies.GraphQL.Contract;
using Fireflies.IoC.Core;
using System.Reflection;

namespace Fireflies.GraphQL.Core;

internal static class AuthorizationHelper {
    public static async Task Authorize(IDependencyResolver dependencyResolver, Type type) {
        var authorizationAttributes = type.GetCustomAttributes<GraphQLAuthorizationAttribute>(true);
        await Authorize(dependencyResolver, authorizationAttributes).ConfigureAwait(false);
    }

    public static async Task Authorize(IDependencyResolver dependencyResolver, MemberInfo memberInfo) {
        var authorizationAttributes = memberInfo.GetCustomAttributes<GraphQLAuthorizationAttribute>(true);
        await Authorize(dependencyResolver, authorizationAttributes).ConfigureAwait(false);
    }

    private static async Task Authorize(IDependencyResolver dependencyResolver, IEnumerable<GraphQLAuthorizationAttribute> authorizationAttributes) {
        if(!authorizationAttributes.Any())
            return;

        foreach(var authorizationAttribute in authorizationAttributes) {
            var authorization = (GraphQLAuthorizationAttribute)dependencyResolver.Resolve(authorizationAttribute.GetType());
            if(await authorization.Authorize().ConfigureAwait(false)) {
                return;
            }
        }

        throw new GraphQLUnauthorizedException();
    }
}