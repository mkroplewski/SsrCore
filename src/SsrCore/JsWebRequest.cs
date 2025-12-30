using Microsoft.AspNetCore.Http;
using Microsoft.JavaScript.NodeApi;

namespace SsrCore;

/// <summary>
/// Represents a Web API Request object that can be converted to JavaScript.
/// </summary>
public class JsWebRequest
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }

    public JsWebRequest() { }

    public JsWebRequest(string url, string method = "GET")
    {
        Url = url;
        Method = method;
    }

    /// <summary>
    /// Creates a JsRequest from an ASP.NET HttpRequest.
    /// </summary>
    public static async Task<JsWebRequest> FromHttpRequest(HttpRequest httpRequest)
    {
        var request = new JsWebRequest
        {
            Url = $"{httpRequest.Scheme}://{httpRequest.Host}{httpRequest.Path}{httpRequest.QueryString}",
            Method = httpRequest.Method,
            Headers = new Dictionary<string, string>()
        };

        // Copy headers
        foreach (var header in httpRequest.Headers)
        {
            request.Headers[header.Key] = header.Value.ToString();
        }

        // Read body for non-GET/HEAD requests
        if (httpRequest.Method != "GET" && httpRequest.Method != "HEAD" && httpRequest.ContentLength > 0)
        {
            using var reader = new StreamReader(httpRequest.Body);
            request.Body = await reader.ReadToEndAsync();
        }

        return request;
    }

    /// <summary>
    /// Converts this .NET object into a real JavaScript 'Request' instance.
    /// </summary>
    public JSValue ToJSValue()
    {
        // 1. Get the real JS constructor from the global scope
        JSValue requestConstructor = JSValue.Global["Request"];
        
        if (requestConstructor.IsUndefined())
        {
            throw new JSException("The 'Request' class was not found in the global JS scope. " +
                                 "Ensure you are running in Node.js 18+ or a browser environment.");
        }

        // 2. Build the 'init' options object for the constructor
        JSObject init = new JSObject();
        init["method"] = Method;

        if (Headers.Count > 0)
        {
            JSObject jsHeaders = new JSObject();
            foreach (var kvp in Headers)
            {
                jsHeaders[kvp.Key] = kvp.Value;
            }
            init["headers"] = jsHeaders;
        }

        if (!string.IsNullOrEmpty(Body) && Method != "GET" && Method != "HEAD")
        {
            init["body"] = Body;
        }

        // 3. Call the constructor: new Request(url, init)
        // This returns a native JS handle to a real Request instance
        return requestConstructor.CallAsConstructor(Url, init);
    }
}
