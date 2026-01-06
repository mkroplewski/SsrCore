using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SsrCore.Services;
using Yarp.ReverseProxy.Forwarder;

public class ViteProxyService : IDisposable
{
    private readonly SsrContextService _ssrContextService;
    private readonly IHttpForwarder _forwarder;
    private readonly HttpMessageInvoker _httpClient;
    private readonly ILogger<ViteProxyService> _logger;

    public ViteProxyService(IHttpForwarder forwarder, SsrContextService ssrContextService, ILogger<ViteProxyService> logger)
    {
        _forwarder = forwarder;
        _ssrContextService = ssrContextService;
        _logger = logger;

        // Use SocketsHttpHandler for performance
        _httpClient = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        }, disposeHandler: true);
    }

    public async Task<bool> ForwardRequest(HttpContext context)
    {
        await _ssrContextService.InitializationTask;
        if (string.IsNullOrEmpty(_ssrContextService.InternalViteUrl)) return false;

        // Forward the request to the internal Node server
        // YARP handles WebSockets (HMR) automatically
        var error = await _forwarder.SendAsync(
            context,
            _ssrContextService.InternalViteUrl,
            _httpClient,
            new ForwarderRequestConfig(),
            ViteHttpTransformer.Instance
        );

        if (error != ForwarderError.None)
        {
            _logger.LogError("Vite proxy error: {Error}. Request: {Method} {Path}, Target: {Target}, Response Status: {StatusCode}",
                error, context.Request.Method, context.Request.Path, _ssrContextService.InternalViteUrl, context.Response.StatusCode);
        }

        return context.Items.ContainsKey("ViteProxyRequestHandled");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}


class ViteHttpTransformer : HttpTransformer
{
    public static readonly ViteHttpTransformer Instance = new();

    public override async ValueTask<bool> TransformResponseAsync(HttpContext context, HttpResponseMessage proxyResponse)
    {
        if (proxyResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        context.Items["ViteProxyRequestHandled"] = true;
        return await base.TransformResponseAsync(context, proxyResponse);
    }
}