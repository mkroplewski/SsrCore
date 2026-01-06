using System.Net;
using Microsoft.AspNetCore.Http;
using SsrCore.Services;
using Yarp.ReverseProxy.Forwarder;

public class ViteProxyService : IDisposable
{
    private readonly SsrContextService _ssrContextService;
    private readonly IHttpForwarder _forwarder;
    private readonly HttpMessageInvoker _httpClient;

    public ViteProxyService(IHttpForwarder forwarder, SsrContextService ssrContextService)
    {
        _forwarder = forwarder;
        _ssrContextService = ssrContextService;

        // Use SocketsHttpHandler for performance
        _httpClient = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        });
    }

    public async Task<bool> ForwardRequest(HttpContext context)
    {
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
            // Handle error (log it)
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