using System.Net.WebSockets;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Json;
using Fireflies.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.IoC.Abstractions;

namespace Fireflies.GraphQL.AspNet;

internal class GraphQLWsProtocolHandler : WsProtocolHandlerBase, IWsProtocolHandler {
    private readonly HttpContext _httpContext;
    private readonly GraphQLEngine _engine;
    private readonly IConnectionContext _connectionContext;
    private readonly IDependencyResolver _requestLifetimeScope;
    private readonly IFirefliesLogger _logger;

    private readonly Dictionary<string, IRequestContext> _subscriptions = new();

    public GraphQLWsProtocolHandler(HttpContext httpContext, GraphQLEngine engine, IConnectionContext connectionContext, IDependencyResolver requestLifetimeScope, IFirefliesLogger logger) : base(httpContext, engine, connectionContext, logger) {
        _httpContext = httpContext;
        _engine = engine;
        _connectionContext = connectionContext;
        _requestLifetimeScope = requestLifetimeScope;
        _logger = logger;
    }

    public async Task Accept() {
        _webSocket = await _httpContext.WebSockets.AcceptWebSocketAsync("graphql-ws").ConfigureAwait(false);
    }

    public async Task HandleResult((string Id, byte[] Result) subResult) {
        var jsonObject = new JsonObject() {
            ["type"] = "data",
            ["id"] = subResult.Id,
            ["payload"] = JsonNode.Parse(subResult.Result)
        };

        await SendAsync(jsonObject, WebSocketMessageType.Text, true, _connectionContext.CancellationToken).ConfigureAwait(false);
    }

    public string? SubProtocol => "graphql-ws";

    public JsonNode HandleFederatedResponse(byte[] bytes, string operationName) {
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        var json = JsonSerializer.Deserialize<JsonObject>(content, DefaultJsonSerializerSettings.DefaultSettings);

        return json?["payload"]?["data"]?[operationName]!;
    }

    protected override async Task ProcessInbound(byte[] bytes) {
        var request = JsonSerializer.Deserialize<JsonNode>(bytes, DefaultJsonSerializerSettings.DefaultSettings);
        switch(request["type"].GetValue<string>()) {
            case "connection_init":
                await ProcessConnectionInit(request).ConfigureAwait(false);
                break;
            case "ping":
                await ProcessPing().ConfigureAwait(false);
                break;
            case "start":
                await ProcessStart(request, bytes).ConfigureAwait(false);
                break;
            case "stop":
                ProcessStop(request);
                break;
        }
    }

    private void ProcessStop(JsonNode request) {
        var id = request["id"].GetValue<string>();
        if(!_subscriptions.TryGetValue(id, out var requestContext))
            return;

        _subscriptions.Remove(id);
        requestContext.Cancel();
    }

    private async Task ProcessStart(JsonNode request, byte[] rawRequest) {
        var id = request["id"].GetValue<string>();
        var payload = request["payload"].Deserialize<GraphQLRequest>(DefaultJsonSerializerSettings.DefaultSettings);
        var requestContext = new RequestContext(_connectionContext, _requestLifetimeScope, id, rawRequest);

        _subscriptions[id] = requestContext;

#pragma warning disable CS4014
        Task.Run(async () => {
            try {
                await _engine.Execute(payload, requestContext).ConfigureAwait(false);
            } catch(Exception ex) {
                _logger.Error(ex, $"Error while running engine.Execute on websocket connection. Path='{_httpContext.Request.Path}'");
            }
        });
#pragma warning restore CS4014
    }

    private async Task ProcessConnectionInit(JsonNode request) {
        var jsonObject = new JsonObject() {
            ["type"] = "connection_ack",
            ["payload"] = null
        };

        await SendAsync(jsonObject, WebSocketMessageType.Text, true, _connectionContext.CancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessPing() {
        var jsonObject = new JsonObject() {
            ["type"] = "pong",
            ["payload"] = null
        };

        await SendAsync(jsonObject, WebSocketMessageType.Text, true, _connectionContext.CancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(JsonNode jsonNode, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) {
        var json = jsonNode.ToJsonString();
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), messageType, endOfMessage, cancellationToken).ConfigureAwait(false);
    }
}