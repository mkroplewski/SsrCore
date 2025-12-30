using Microsoft.JavaScript.NodeApi;

namespace SsrCore.Interfaces;

/// <summary>
/// Represents a Web API Response object that can be marshalled from JavaScript.
/// </summary>
[JSImport]
public interface IJsWebResponse
{
    int Status { get; }
    string StatusText { get; }
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
    string? Get(string name);
    void Set(string name, string value);
    bool Has(string name);
    void Delete(string name);
    IEnumerable<string[]> Entries();
}
