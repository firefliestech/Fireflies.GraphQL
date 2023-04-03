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
            await _next(httpContext);
            return;
        }

        httpContext.Response.StatusCode = (int)HttpStatusCode.OK;

        if(httpContext.Request.Method == "OPTIONS")
            return;

        var requestLifetimeScope = CreateRequestLifetimeScope(httpContext);
        var engine = requestLifetimeScope.Resolve<GraphQLEngine>();
        var options = requestLifetimeScope.Resolve<GraphQLContext>();
        var loggerFactory = requestLifetimeScope.Resolve<IFirefliesLoggerFactory>();
        var logger = loggerFactory.GetLogger<GraphQLMiddleware>();

        try {
            if(httpContext.WebSockets.IsWebSocketRequest) {
                options.WebSocket = await httpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                ProcessWebSocket(httpContext, engine, logger);
            } else {
                var request = await JsonSerializer.DeserializeAsync<GraphQLRequest>(httpContext.Request.Body, DefaultJsonSerializerSettings.DefaultSettings).ConfigureAwait(false);
                await engine.Execute(request).ConfigureAwait(false);
            }

            await foreach(var subResult in engine.Results().WithCancellation(engine.Context.CancellationToken).ConfigureAwait(false)) {
                if(engine.Context.IsWebSocket) {
                    await engine.Context.WebSocket!.SendAsync(new ArraySegment<byte>(subResult, 0, subResult.Length), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                } else {
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.Body.WriteAsync(subResult, 0, subResult.Length);
                }
            }
        } catch(OperationCanceledException) {
            // Noop
        } catch(Exception ex) {
            logger.Error(ex, "Exception occured while processing request");
        } finally {
            if(engine.Context.IsWebSocket) {
                try {
                    await engine.Context.WebSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                } catch {
                    // Noop
                }
            }

            requestLifetimeScope.Dispose();
        }
    }

    private IDependencyResolver CreateRequestLifetimeScope(HttpContext httpContext) {
        var graphQLContext = new GraphQLContext(httpContext);

        var lifetimeScopeResolver = _options.DependencyResolver.BeginLifetimeScope(builder => {
            builder.RegisterType<GraphQLEngine>();
            builder.RegisterInstance(_options.LoggerFactory);
            builder.RegisterInstance(httpContext);
            builder.RegisterInstance(graphQLContext);
            builder.RegisterInstance((IGraphQLContext)graphQLContext);
            if(_options.DependencyResolver.TryResolve<IRequestDependencyResolverBuilder>(out var innerBuilder)) {
                innerBuilder!.Build(builder, httpContext);
            }

            _options.Extensions.BuildRequestLifetimeScope(builder);
        });
        return lifetimeScopeResolver;
    }

    private async void ProcessWebSocket(HttpContext httpContext, GraphQLEngine engine, IFirefliesLogger firefliesLogger) {
        firefliesLogger.Debug($"Connection from {httpContext.Connection.RemoteIpAddress}:{httpContext.Connection.RemotePort} to {httpContext.Request.Path} opened");

        while(true) {
            try {
                var (webSocketReceiveResult, bytes) = await ReceiveFullMessage(engine.Context.WebSocket!, engine.Context.CancellationToken).ConfigureAwait(false);
                if(webSocketReceiveResult.MessageType == WebSocketMessageType.Close) {
                    firefliesLogger.Debug($"Connection from {httpContext.Connection.RemoteIpAddress}:{httpContext.Connection.RemotePort} to {httpContext.Request.Path} closed");
                    await engine.Context.WebSocket!.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
                    break;
                }

                var request = JsonSerializer.Deserialize<GraphQLRequest>(bytes, DefaultJsonSerializerSettings.DefaultSettings);
#pragma warning disable CS4014
                Task.Run(async () => {
                    try {
                        await engine.Execute(request).ConfigureAwait(false);
                    } catch(Exception ex) {
                        firefliesLogger.Error(ex, $"Error while running engine.Execute on websocket connection. Path='{httpContext.Request.Path}'");
                    }
                });
#pragma warning restore CS4014
            } catch(Exception ex) {
                firefliesLogger.Error(ex, $"Error while running OnReceived connection. Path='{httpContext.Request.Path}'");
                break;
            }
        }

        engine.Context.Done();
    }

    private async Task<(WebSocketReceiveResult, byte[])> ReceiveFullMessage(WebSocket socket, CancellationToken cancelToken) {
        WebSocketReceiveResult response;
        var message = new List<byte>();

        var buffer = new byte[4096];
        do {
            response = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancelToken).ConfigureAwait(false);
            message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
        } while(!response.EndOfMessage && response.MessageType != WebSocketMessageType.Close);

        return (response, message.ToArray());
    }
}