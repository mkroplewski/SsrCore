using System.Buffers;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.JavaScript.NodeApi;
using SsrCore.Interfaces;

namespace SsrCore.Services;

public class RenderService
{
    private readonly SsrCoreOptions _options;
    private readonly NodeService _nodeService;
    private readonly SsrContextService _ssrContextService;

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
        var writingTask = CopyPipeToResponseAsync(pipe.Reader, context.Response.Body);

        // 4. Run the Producer (JS Thread)
        // We await this to ensure the JS generation completes or errors out.
        await _ssrContextService.Runtime.RunAsync(async () =>
        {
            try
            {
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
                Stream stream = _options.RenderMode == RenderMode.WebReadableStream
                    ? _ssrContextService.NodeReadable.FromWeb(body.Value)
                    : _nodeService.Marshaller.FromJS<Stream>(body.Value);

                // 7. Critical Optimization Loop
                var writer = pipe.Writer;
                while (true)
                {
                    // Get memory directly from the Pipe. No 'new byte[]' allocations.
                    // We ask for at least 4KB, but the Pipe might give us more.
                    Memory<byte> memory = writer.GetMemory(4096);

                    // Pass the Pipe's memory directly to the NodeStream.
                    // NodeStream marshals this as a TypedArray. If supported by the interop, 
                    // V8 writes directly into this pinned .NET memory.
                    int bytesRead = await stream.ReadAsync(memory).ConfigureAwait(true);

                    if (bytesRead == 0)
                    {
                        break; // End of stream
                    }

                    // Tell the pipe how much we actually wrote
                    writer.Advance(bytesRead);

                    // Flush makes the data available to the writingTask.
                    // FlushAsync returns a FlushResult. If IsCompleted/IsCanceled, client disconnected.
                    // This handles BACKPRESSURE. If the client is slow, FlushAsync pauses here,
                    // stopping the JS loop from generating more data until space clears.
                    var result = await writer.FlushAsync().ConfigureAwait(true);

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        break;
                    }
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

    // Separate helper method to keep the main logic clean.
    // This runs on the ThreadPool, freeing up the JS Thread.
    private static async Task CopyPipeToResponseAsync(PipeReader reader, Stream responseBody)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (buffer.IsEmpty && result.IsCompleted)
            {
                break;
            }

            // Efficiently write the sequence to the response stream
            foreach (var segment in buffer)
            {
                await responseBody.WriteAsync(segment);
            }

            // Tell the pipe we processed everything
            reader.AdvanceTo(buffer.End);

            // Explicit flush to ensure chunks get sent to client immediately (Streaming UI)
            await responseBody.FlushAsync();

            if (result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
    }

}