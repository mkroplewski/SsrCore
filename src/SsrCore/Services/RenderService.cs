using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Interop;
using SsrCore.Interfaces;

namespace SsrCore.Services;

public class RenderService
{
    private readonly SsrCoreOptions _options;
    private readonly NodeService _nodeService;
    private readonly SsrContextService _ssrContextService;
    
    // Cache generic method info globally
    private static readonly MethodInfo _wrapMethod = typeof(JSRuntimeContext)
        .GetMethod(nameof(JSRuntimeContext.GetOrCreateObjectWrapper))!;

    // Cache constructed generic methods to ensure zero-allocation lookups
    private static readonly ConcurrentDictionary<Type, MethodInfo> _wrapMethodCache = new();

    /// <summary>
    /// Initializes a new instance of <see cref="RenderService"/> with the provided SSR configuration and required services.
    /// </summary>
    /// <param name="options">Application configuration for server-side rendering.</param>
    /// <param name="nodeService">Service used for JavaScript/Node interop and marshalling.</param>
    /// <param name="ssrContextService">Service that provides the SSR runtime context and entry-function resolution.</param>
    public RenderService(IOptions<SsrCoreOptions> options, NodeService nodeService, SsrContextService ssrContextService)
    {
        _nodeService = nodeService;
        _ssrContextService = ssrContextService;
        _options = options.Value;
    }

    /// <summary>
    /// Orchestrates server-side rendering for an HTTP request by invoking the configured JS entry, setting status and headers, and writing the rendered output to the response (either as an atomic HTML string or a streamed body).
    /// </summary>
    /// <param name="context">The HTTP context for the incoming request and response.</param>
    /// <returns>A task that completes when rendering and response streaming have finished; faults if rendering fails.</returns>
    public async Task Render(HttpContext context)
    {
        // 1. Convert Request
        // Optimization: Do this before switching context to minimize time spent on JS thread
        var jsRequest = await JsWebRequest.FromHttpRequest(context.Request);

        // 2. Create a Pipe for high-performance streaming
        var pipe = new Pipe();

        // 3. Start the Consumer Task (NET ThreadPool)
        // This reads from the pipe and writes to the HTTP Response.
        // We start this immediately so it's ready when data arrives.
        var writingTask = pipe.Reader.CopyToAsync(context.Response.Body, context.RequestAborted);

        // 4. Run the Producer (JS Thread)
        // We await this to ensure the JS generation completes or errors out.
        await _ssrContextService.Runtime.RunAsync(async () =>
        {
            try
            {
                //Service Injection
                foreach (var inject in _options.Services.Injects)
                {
                    var service = context.RequestServices.GetRequiredService(inject.InterfaceType);
                    // Wrap Dynamically
                    var method = _wrapMethodCache.GetOrAdd(inject.InterfaceType, t => _wrapMethod.MakeGenericMethod(t));

                    var proxy = (JSValue)method.Invoke(JSRuntimeContext.Current, [service])!;

                    JSValue.Global.SetProperty(inject.JsName ?? inject.InterfaceType.Name, proxy);
                }
                
                
                var entry = await _ssrContextService.GetEntryFunctionAsync();
                var jsRequestValue = jsRequest.ToJSValue();
                var jsResponseValue = entry.Call(JSValue.Undefined, jsRequestValue);
                if (jsResponseValue.IsPromise())
                {
                    jsResponseValue = await ((JSPromise)jsResponseValue).AsTask();
                }
                var jsResponse = _nodeService.Marshaller.FromJS<IJsWebResponse>(jsResponseValue);

                // 5. Set Status and Headers
                // Must happen before writing to the pipe/response body
                context.Response.StatusCode = jsResponse.Status;
                foreach (var header in jsResponse.Headers.Entries())
                {
                    // Note: Set headers directly, avoiding dictionary overhead if possible
                    context.Response.Headers[header[0]] =
                        new StringValues(header[1]);
                }

                // Optimization: Fast path for string rendering
                if (_options.RenderMode == RenderMode.String)
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    var text = await jsResponse.Text();
                    // Direct write, no pipe needed for atomic strings
                    await context.Response.WriteAsync(text);
                    return;
                }

                // 6. Streaming Logic
                var body = jsResponse.Body;
                if (body == null) throw new InvalidOperationException("Response body is null");

                // Get the stream wrapper based on mode
                Stream nodeStream = _options.RenderMode == RenderMode.WebReadableStream
                    ? _ssrContextService.NodeReadable.FromWeb(body.Value)
                    : _nodeService.Marshaller.FromJS<Stream>(body.Value);

                // 7. Critical Optimization Loop
                var writer = pipe.Writer;
                while (true)
                {
                    // Ask Pipe for a chunk of memory
                    Memory<byte> memory = writer.GetMemory(8192); // 8KB chunks

                    // JS writes directly into that memory
                    int bytesRead = await nodeStream.ReadAsync(memory);
                    if (bytesRead == 0) break;

                    writer.Advance(bytesRead);

                    // FlushResult tells us if the Consumer is keeping up.
                    // If result.IsCompleted, the browser disconnected.
                    var result = await writer.FlushAsync();
                    if (result.IsCompleted || result.IsCanceled) break;
                }
            }
            catch (Exception ex)
            {
                // Propagate error to the pipe reader so the request fails correctly
                await pipe.Writer.CompleteAsync(ex);
                throw; // Re-throw to log in the JS runner context
            }
            finally
            {
                // Clean up injected services
                foreach (var inject in _options.Services.Injects)
                {
                    JSValue.Global.DeleteProperty(inject.JsName ?? inject.InterfaceType.Name);
                }
                // Signal we are done producing
                await pipe.Writer.CompleteAsync();
            }
        });

        // 8. Ensure Consumer finishes
        await writingTask;
    }

}