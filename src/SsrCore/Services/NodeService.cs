using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Runtime;
using SsrCore.Interfaces;

namespace SsrCore.Services;

public class NodeService
{
    private readonly SsrCoreOptions _options;
    internal readonly NodeEmbeddingPlatform Platform;
    internal readonly JSMarshaller Marshaller = new JSMarshaller()
    {
        AutoCamelCase = true,
    };

    public NodeService(IOptions<SsrCoreOptions> options)
    {
        _options = options.Value;

        string baseDir = AppContext.BaseDirectory;

        string rid = RuntimeInformation.RuntimeIdentifier;
        // Logic to locate libnode.dll trying multiple common locations
        string libnodePath = Path.Combine(baseDir, "runtimes", rid, "native", "libnode.dll");
        if (!File.Exists(libnodePath))
        {
            libnodePath = Path.Combine(baseDir, "libnode.dll");
        }

        if (!File.Exists(libnodePath))
        {
            throw new FileNotFoundException($"Could not find libnode.dll at {libnodePath}");
        }

        var settings = new NodeEmbeddingPlatformSettings
        {
            LibNodePath = libnodePath
        };

        Platform = new NodeEmbeddingPlatform(settings);
    }
}