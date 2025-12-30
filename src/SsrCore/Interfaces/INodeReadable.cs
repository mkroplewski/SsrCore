using Microsoft.JavaScript.NodeApi;

namespace SsrCore.Interfaces;

/// <summary>
/// Interface for Node.js Readable stream utilities.
/// </summary>
[JSImport]
public interface INodeReadable
{
    /// <summary>
    /// Converts a Web ReadableStream to a Node.js Readable stream (marshalled as .NET Stream).
    /// </summary>
    /// <param name="webReadableStream">The Web ReadableStream.</param>
    /// <returns>A .NET Stream wrapper.</returns>
    [JSExport("fromWeb")]
    Stream FromWeb(JSValue webReadableStream);
}