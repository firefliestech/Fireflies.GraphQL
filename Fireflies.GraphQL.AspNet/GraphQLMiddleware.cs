using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Json;
using Fireflies.IoC.Abstractions;
using Fireflies.Logging.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Fireflies.GraphQL.AspNet;

public class GraphQLMiddleware {
    private readonly GraphQLOptions _options;
    private readonly RequestDelegate _next;

    public GraphQLMiddleware(GraphQLOptions options, RequestDelegate next) {
        _options = options;
        _next = next;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task InvokeAsync(HttpContext httpContext) {
        if(!httpContext.Request.Path.StartsWithSegments(_options.Url)) {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        httpContext.Response.StatusCode = (int)HttpStatusCode.OK;

        if(httpContext.Request.Method == "OPTIONS")
            return;

        var connectionContext = new ConnectionContext(httpContext);
        
        var requestLifetimeFactory = _options.DependencyResolver.Resolve<RequestContainerFactory>();
        var requestLifetimeScope = requestLifetimeFactory.Create(connectionContext);

        var engine = requestLifetimeScope.Resolve<GraphQLEngine>();
        var loggerFactory = requestLifetimeScope.Resolve<IFirefliesLoggerFactory>();
        var logger = loggerFactory.GetLogger<GraphQLMiddleware>();

        try {
            if(httpContext.WebSockets.IsWebSocketRequest) {
                var protocolHandler = GetSubProtocolHandler(httpContext, engine, connectionContext, loggerFactory, requestLifetimeScope);
                await protocolHandler.Accept();
                connectionContext.WebSocket = protocolHandler;
                connectionContext.WebSocket.Process();
            } else {
                var request = await JsonSerializer.DeserializeAsync<GraphQLRequest>(httpContext.Request.Body, DefaultJsonSerializerSettings.DefaultSettings).ConfigureAwait(false);
                await engine.Execute(request, new RequestContext(connectionContext, requestLifetimeScope)).ConfigureAwait(false);
            }

            await foreach(var subResult in engine.Results().WithCancellation(connectionContext.CancellationToken).ConfigureAwait(false)) {
                if(connectionContext.IsWebSocket) {
                    await connectionContext.WebSocket!.HandleResult(subResult);
                } else {
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.Body.WriteAsync(subResult.Result, 0, subResult.Result.Length).ConfigureAwait(false);
                }
            }
        } catch(OperationCanceledException) {
            // Noop
        } catch(Exception ex) {
            logger.Error(ex, "Exception occured while processing request");
        } finally {
            if(connectionContext.IsWebSocket) {
                try {
                    await connectionContext.WebSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                } catch {
                    // Noop
                }
            }

            requestLifetimeScope.Dispose();
        }
    }

    private IWsProtocolHandler GetSubProtocolHandler(HttpContext httpContext, GraphQLEngine engine, IConnectionContext connectionContext, IFirefliesLoggerFactory loggerFactory, IDependencyResolver requestLifetimeScope) {
        if(httpContext.WebSockets.WebSocketRequestedProtocols.Any(protocol => protocol == "graphql-ws")) {
            return new GraphQLWsProtocolHandler(httpContext, engine, connectionContext, requestLifetimeScope, loggerFactory.GetLogger<GraphQLWsProtocolHandler>());
        }

        if(httpContext.WebSockets.WebSocketRequestedProtocols.Any())
            throw new ArgumentOutOfRangeException(nameof(httpContext.WebSockets.WebSocketRequestedProtocols), $"Unknown sub-protocol ({string.Join(",", httpContext.WebSockets.WebSocketRequestedProtocols)})");

        return new DefaultWsProtocolHandler(httpContext, engine, connectionContext, requestLifetimeScope, loggerFactory.GetLogger<DefaultWsProtocolHandler>());
    }
}
