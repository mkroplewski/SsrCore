using Microsoft.JavaScript.NodeApi;

namespace SsrCore.Interfaces;

/// <summary>
/// Represents a Web API Response object that can be marshalled from JavaScript.
/// </summary>
[JSImport]
public interface IJsWebResponse
{
    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    int Status { get; }

    /// <summary>
    /// Gets the HTTP status text.
    /// </summary>
    string StatusText { get; }

    /// <summary>
    /// Gets the response headers.
    /// </summary>
    IJsHeaders Headers { get; }
    
    /// <summary>
    /// Gets the ReadableStream body of the response (marshalled as Stream).
    /// </summary>
    JSValue? Body { get; }

    /// <summary>
    /// Returns the response body as text.
    /// </summary>
    Task<string> Text();

    /// <summary>
    /// Returns the response body as an ArrayBuffer (byte array).
    /// </summary>
    Task<byte[]> ArrayBuffer();
}

/// <summary>
/// Represents Web API Headers object.
/// </summary>
[JSImport]
public interface IJsHeaders
{
    /// <summary>
    /// Gets the value of a header.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>The header value, or null if not found.</returns>
    string? Get(string name);

    /// <summary>
    /// Sets a header value.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    void Set(string name, string value);

    /// <summary>
    /// Checks if a header exists.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>True if the header exists, false otherwise.</returns>
    bool Has(string name);

    /// <summary>
    /// Deletes a header.
    /// </summary>
    /// <param name="name">The header name.</param>
    void Delete(string name);

    /// <summary>
    /// Returns an iterator of all headers as string arrays (key-value pairs).
    /// </summary>
    /// <returns>An enumerable of key-value pairs.</returns>
    IEnumerable<string[]> Entries();
}
