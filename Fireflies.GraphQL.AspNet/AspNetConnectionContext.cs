using System.Net;
using Fireflies.GraphQL.Core;
using Fireflies.IoC.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Fireflies.GraphQL.AspNet;

internal class AspNetConnectionContext : IConnectionContext<HttpContext> {
    private readonly GraphQLOptions _options;

    private readonly CancellationTokenSource _cancellationTokenSource;

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public IDependencyResolver ConnectionDependencyResolver { get; internal set; } = null!;

    public HttpContext HttpContext { get; }
    public Dictionary<string, string[]> RequestHeaders => HttpContext.Request.Headers.Where(x => !x.Key.StartsWith(":")).ToDictionary(x => x.Key, x => x.Value.ToArray());
    public string QueryString => HttpContext.Request.QueryString.Value;

    public IResultBuilder Results { get; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public bool IsWebSocket => WebSocket != null;
    public IWsProtocolHandler? WebSocket { get; internal set; }

    public AspNetConnectionContext(HttpContext httpContext, GraphQLOptions options) {
        _options = options;
        _cancellationTokenSource = new();

        HttpContext = httpContext;
        Results = new ResultBuilder(HttpContext.WebSockets.IsWebSocketRequest, _cancellationTokenSource);
    }

    private AspNetConnectionContext(AspNetConnectionContext parent) {
        _options = parent._options;
        _cancellationTokenSource = parent._cancellationTokenSource;

        HttpContext = parent.HttpContext;
        Results = new ResultBuilder(HttpContext.WebSockets.IsWebSocketRequest, _cancellationTokenSource);
        ConnectionDependencyResolver = parent.ConnectionDependencyResolver;
    }

    public IConnectionContext CreateChildContext() {
        return new AspNetConnectionContext(this);
    }

    public IDependencyResolver CreateRequestContainer() {
        var lifetimeScopeResolver = ConnectionDependencyResolver.BeginLifetimeScope(builder => {
            builder.RegisterType<GraphQLEngine>();
            builder.RegisterInstance((IConnectionContext)this);
            if(ConnectionDependencyResolver.TryResolve<IRequestContainerBuilder<HttpContext>>(out var innerBuilder)) {
                innerBuilder!.Build(builder, HttpContext);
            }

            _options.Extensions.BuildRequestLifetimeScope(builder);
        });

        return lifetimeScopeResolver;
    }
}