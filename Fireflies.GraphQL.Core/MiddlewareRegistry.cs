using Fireflies.GraphQL.Core.Middleware;
using Fireflies.GraphQL.Core.Middleware.Sorting;

namespace Fireflies.GraphQL.Core;

internal static class MiddlewareRegistry {
    private static List<IMiddleware> Registered = new();

    public static IEnumerable<IMiddleware> GetMiddlewares() {
        return Registered;
    }

    static MiddlewareRegistry() {
        Registered.Add(new SortingMiddleware());
    }
}