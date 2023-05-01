using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Json;
using Fireflies.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.AspNet;

internal class DefaultWsProtocolHandler : WsProtocolHandlerBase, IWsProtocolHandler {
    private readonly HttpContext _httpContext;
    private readonly GraphQLEngine _engine;
    private readonly IConnectionContext _connectionContext;
    private readonly IDependencyResolver _requestLifetimeScope;
    private readonly IFirefliesLogger _logger;

    public DefaultWsProtocolHandler(HttpContext httpContext, GraphQLEngine engine, IConnectionContext connectionContext, IDependencyResolver requestLifetimeScope, IFirefliesLogger logger) : base(httpContext, engine, connectionContext, logger) {
        _httpContext = httpContext;
        _engine = engine;
        _connectionContext = connectionContext;
        _requestLifetimeScope = requestLifetimeScope;
        _logger = logger;
    }

    public async Task Accept() {
        _webSocket = await _httpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
    }

    public async Task HandleResult((string Id, byte[] Result) subResult) {
        await SendAsync(new ArraySegment<byte>(subResult.Result, 0, subResult.Result.Length), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }

    public string? SubProtocol => null;
    
    public JsonNode? HandleFederatedResponse(byte[] bytes, string operationName) {
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        var json = JsonSerializer.Deserialize<JsonObject>(content, DefaultJsonSerializerSettings.DefaultSettings);

        if(json?[operationName] == null)
            return null;

        return json![operationName];
    }

    protected override Task ProcessInbound(byte[] bytes) {
        var request = JsonSerializer.Deserialize<GraphQLRequest>(bytes, DefaultJsonSerializerSettings.DefaultSettings);
#pragma warning disable CS4014
        Task.Run(async () => {
            try {
                await _engine.Execute(request, new RequestContext(_connectionContext, _requestLifetimeScope, null, bytes)).ConfigureAwait(false);
            } catch(Exception ex) {
                _logger.Error(ex, $"Error while running engine.Execute on websocket connection. Path='{_httpContext.Request.Path}'");
            }
        });
#pragma warning restore CS4014

        return Task.CompletedTask;
    }
}