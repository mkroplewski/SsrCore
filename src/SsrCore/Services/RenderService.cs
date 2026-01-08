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

    public RenderService(IOptions<SsrCoreOptions> options, NodeService nodeService, SsrContextService ssrContextService)
    {
        _nodeService = nodeService;
        _ssrContextService = ssrContextService;
        _options = options.Value;
    }

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



                var entry = await _ssrContextService.GetEntryFunctionAsync();
                var jsRequestValue = jsRequest.ToJSValue();
                var services = new JSObject();
                //Service Injection
                foreach (var inject in _options.Services.Injects)
                {
                    var service = context.RequestServices.GetRequiredService(inject.InterfaceType);
                    // Wrap Dynamically
                    var method = _wrapMethodCache.GetOrAdd(inject.InterfaceType, t => _wrapMethod.MakeGenericMethod(t));

                    var proxy = (JSValue)method.Invoke(JSRuntimeContext.Current, [service])!;

                    services.Add(inject.JsName ?? inject.InterfaceType.Name, proxy);
                }

                var jsResponseValue = entry.Call(JSValue.Undefined, jsRequestValue, services);
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
                // Signal we are done producing
                await pipe.Writer.CompleteAsync();
            }
        });

        // 8. Ensure Consumer finishes
        await writingTask;
    }

}