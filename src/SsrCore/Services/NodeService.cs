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
    private readonly NodeEmbeddingPlatform _platform;
    internal readonly NodeEmbeddingThreadRuntime Runtime;
    internal readonly JSMarshaller Marshaller = new JSMarshaller()
    {
        AutoCamelCase = true,
    };
    internal JSReference EntryServer;
    internal INodeReadable NodeReadable;

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

        _platform = new NodeEmbeddingPlatform(settings);

        Runtime = _platform.CreateThreadRuntime(baseDir, new NodeEmbeddingRuntimeSettings
        {
            // Initialize the require function so we can load modules
            MainScript = "globalThis.require = require('module').createRequire(process.execPath);\n"
        });

        string bundlePath = Path.Combine(baseDir, "wwwroot", "server", "entry-server.mjs");
        
        Task.Run(() =>
            Runtime.RunAsync(async () =>
            {
                var mod = await Runtime.ImportAsync(bundlePath, null, true);
                EntryServer = new JSReference(mod);

                // Cache the Readable class for later use
                var radableModule = await Runtime.ImportAsync("stream", "Readable");
                NodeReadable = Marshaller.FromJS<INodeReadable>(radableModule);
            })).Wait();
    }
}