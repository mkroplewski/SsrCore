using Microsoft.JavaScript.NodeApi;

namespace SsrCore.Interfaces;

/// <summary>
/// Interface for the JavaScript entry server.
/// </summary>
[JSImport]
public interface IEntryServer
{
    /// <summary>
    /// The default export function of the server bundle.
    /// </summary>
    /// <param name="request">The JS Request object.</param>
    /// <returns>A JS Response object.</returns>
    public Task<IJsWebResponse> Default(JSValue request);
}