using System.Net;
using System.Net.WebSockets;
using Fireflies.GraphQL.Core;
using Fireflies.IoC.Abstractions;
using Fireflies.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

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
                options.WebSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                ProcessWebSocket(httpContext, engine, logger);
            } else {
                var input = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<GraphQLRequest>(input);
                await engine.Execute(request);
            }

            await foreach(var subResult in engine.Results().WithCancellation(engine.Context.CancellationToken)) {
                if(engine.Context.IsWebSocket) {
                    var buffer = System.Text.Encoding.UTF8.GetBytes(subResult);
                    await engine.Context.WebSocket!.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                } else {
                    await httpContext.Response.WriteAsync(subResult);
                }
            }
        } catch(OperationCanceledException) {
            // Noop
        } catch(Exception ex) {
            logger.Error(ex, "Exception occured while processing request");
        } finally {
            if(engine.Context.IsWebSocket) {
                try {
                    await engine.Context.WebSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                } catch {
                    // Noop
                }
            }

            requestLifetimeScope.Dispose();
        }
    }

    private IDependencyResolver CreateRequestLifetimeScope(HttpContext httpContext) {
        return _options.DependencyResolver.BeginLifetimeScope(builder => {
            builder.RegisterInstance(_options.LoggerFactory);
            builder.RegisterInstance(httpContext);
            var graphQLContext = new GraphQLContext(httpContext);
            builder.RegisterInstance(graphQLContext);
            builder.RegisterInstance((IGraphQLContext)graphQLContext);
            if(_options.DependencyResolver.TryResolve<IRequestDependencyResolverBuilder>(out var innerBuilder)) {
                innerBuilder!.Build(builder, httpContext);
            }
        });
    }

    private async void ProcessWebSocket(HttpContext httpContext, GraphQLEngine engine, IFirefliesLogger firefliesLogger) {
        firefliesLogger.Debug($"Connection from {httpContext.Connection.RemoteIpAddress}:{httpContext.Connection.RemotePort} to {httpContext.Request.Path} opened");

        while(true) {
            try {
                var (webSocketReceiveResult, bytes) = await ReceiveFullMessage(engine.Context.WebSocket!, engine.Context.CancellationToken).ConfigureAwait(false);
                if(webSocketReceiveResult.MessageType == WebSocketMessageType.Close) {
                    firefliesLogger.Debug($"Connection from {httpContext.Connection.RemoteIpAddress}:{httpContext.Connection.RemotePort} to {httpContext.Request.Path} closed");
                    await engine.Context.WebSocket!.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    break;
                }

                var request = JsonConvert.DeserializeObject<GraphQLRequest>(System.Text.Encoding.UTF8.GetString(bytes));
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