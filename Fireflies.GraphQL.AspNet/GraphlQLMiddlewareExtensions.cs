using Fireflies.GraphQL.Core;
using Microsoft.AspNetCore.Builder;

namespace Fireflies.GraphQL.AspNet;

public static class GraphlQLMiddlewareExtensions {
    public static IApplicationBuilder UseGraphQL(this IApplicationBuilder builder, GraphQLOptions? options = null) {
        return builder.UseMiddleware<GraphQLMiddleware>(options ?? new GraphQLOptions());
    }
}