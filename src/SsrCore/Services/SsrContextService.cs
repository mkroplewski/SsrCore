using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;
using SsrCore.Interfaces;

namespace SsrCore.Services;

public class SsrContextService
{
    private readonly IOptions<SsrCoreOptions> _options;
    private readonly bool _isDevelopment;
    
    private JSReference _ssrLoadModule;
    private JSReference EntryServer;
    
    internal INodeReadable NodeReadable;
    internal NodeEmbeddingThreadRuntime Runtime;
    internal string InternalViteUrl;

    public SsrContextService(NodeService nodeService, IOptions<SsrCoreOptions> options, IWebHostEnvironment env)
    {
        var nodeService1 = nodeService;
        _options = options;
        _isDevelopment = env.IsDevelopment();

        var frontendDistPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "server");
        var frontendPath = Path.Combine(env.ContentRootPath, options.Value.FrontendPath);
        var baseDir = env.IsDevelopment() ? frontendPath : frontendDistPath;

        Runtime = nodeService1.Platform.CreateThreadRuntime(baseDir, new NodeEmbeddingRuntimeSettings
        {
            // Initialize the require function so we can load modules
            MainScript = "globalThis.require = require('module').createRequire(process.execPath);\n"
        });

        string bundlePath = Path.Combine(baseDir, "entry-server.mjs");

        Task.Run(() =>
            Runtime.RunAsync(async () =>
            {
                if (env.IsProduction())
                {
                    var mod = await Runtime.ImportAsync(bundlePath, null, true);
                    EntryServer = new JSReference(mod);
                }
                else
                {
                    var module = await Runtime.ImportAsync("./vite-server.js", esModule: true);

                    // Call startVite and get the URL back
                    var promise = (JSPromise)module.CallMethod("default");
                    var viteReturn = await promise.AsTask();
                    // var viteReturn = task.GetAwaiter().GetResult();
                    InternalViteUrl = (string)viteReturn.GetProperty("url");
                    _ssrLoadModule = new JSReference(viteReturn.GetProperty("ssrLoadModule"));
                }

                // Cache the Readable class for later use
                var readableModule = await Runtime.ImportAsync("stream", "Readable");
                NodeReadable = nodeService1.Marshaller.FromJS<INodeReadable>(readableModule);
            })).Wait();
    }

    public async Task<JSReference> GetDevEntryServer()
    {
        if (_ssrLoadModule == null)
        {
            throw new InvalidOperationException("SSR Load Module is not initialized.");
        }

        var promise = (JSPromise)_ssrLoadModule.GetValue()
            .Call(JSValue.Undefined, Path.Combine("src", "entry-server.tsx"));

        var result = await promise.AsTask();
        return new JSReference(result);
    }

    public async Task<JSValue> GetEntryFunctionAsync()
    {
        JSValue entry;
        if (_isDevelopment)
        {
            var devEntryRef = await GetDevEntryServer();
            // We can dispose the reference immediately after getting a value in the current scope.
            // The value handle in the current scope will keep the object alive.
            using (devEntryRef)
            {
                entry = devEntryRef.GetValue();
            }
        }
        else
        {
            entry = EntryServer.GetValue();
        }

        var entryFunctionOption = _options.Value.EntryFunction;
        var entryFunction = entry;

        foreach (var part in entryFunctionOption.Split('.'))
        {
            entryFunction = entryFunction[part];
        }

        return entryFunction;
    }
}