namespace SsrCore;

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
    /// Defaults to "Frontend".
    /// </summary>
    public string FrontendPath { get; set; } = "Frontend";

    /// <summary>
    /// Gets the service injection configuration.
    /// </summary>
    public SsrCoreServices Services { get; } = new();

}

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
/// Configuration for services to be injected into the Node.js environment.
/// </summary>
public class SsrCoreServices
{
    internal List<NodeServiceRegistration> Injects { get; } = new();

    /// <summary>
    /// Registers a service interface to be injected into the Node.js environment.
    /// The injected service will be passed as the second argument to the render function during SSR.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type.</typeparam>
    /// <param name="jsName">Optional JavaScript name for the service. If null, the .NET type name is used.</param>
    public void Inject<TInterface>(string? jsName = null) where TInterface : class
    {
        Injects.Add(new NodeServiceRegistration(
            jsName,
            typeof(TInterface)
        ));
    }
}

internal record NodeServiceRegistration(
    string? JsName,
    Type InterfaceType
);