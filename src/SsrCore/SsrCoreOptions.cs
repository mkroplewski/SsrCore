/// <summary>
/// Specifies the rendering mode for the Node.js SSR process.
/// </summary>
public enum RenderMode
{
    /// <summary>
    /// Renders to a simple string.
    /// </summary>
    String,

    /// <summary>
    /// Renders using a standard Web API ReadableStream.
    /// </summary>
    WebReadableStream,

    /// <summary>
    /// Renders using a Node.js Readable stream.
    /// </summary>
    NodeReadableStream
}

/// <summary>
/// Options for configuring SsrCore.
/// </summary>
public class SsrCoreOptions
{
    /// <summary>
    /// Gets or sets the rendering mode. Defaults to <see cref="RenderMode.String"/>.
    /// </summary>
    public RenderMode RenderMode { get; set; } = RenderMode.String;

    /// <summary>
    /// Gets or sets the entry function name in the JavaScript bundle.
    /// Defaults to "default". Supports dot-notation for nested functions (e.g., "entry.module.default").
    /// </summary>
    public string EntryFunction { get; set; } = "default";

    /// <summary>
    /// Gets or sets the path to the frontend directory (containing vite.config.js).
    /// Defaults to "Client".
    /// </summary>
    public string FrontendPath { get; set; } = "Frontend";
    
}