using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Runtime;
using SsrCore.Interfaces;

namespace SsrCore.Services;

public class SsrContextService : IHostedService
{
    private readonly IOptions<SsrCoreOptions> _options;
    private readonly NodeService _nodeService;
    private readonly IWebHostEnvironment _env;
    private readonly TaskCompletionSource _initializedTcs = new();
    private readonly ILogger<SsrContextService> _logger;
    private readonly bool _isDevelopment;
    private readonly string _bundlePath;

    private JSReference? _ssrLoadModule;
    private JSReference? EntryServer;

    internal INodeReadable NodeReadable = null!; // Initialized in InitializeAsync
    internal NodeEmbeddingThreadRuntime Runtime;
    internal string? InternalViteUrl;

    public Task InitializationTask => _initializedTcs.Task;

    public SsrContextService(NodeService nodeService, IOptions<SsrCoreOptions> options, IWebHostEnvironment env, ILogger<SsrContextService> logger)
    {
        _nodeService = nodeService;
        _options = options;
        _env = env;
        _logger = logger;
        _isDevelopment = env.IsDevelopment();

        var frontendDistPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "server");
        var frontendPath = Path.Combine(env.ContentRootPath, options.Value.FrontendPath);
        var baseDir = env.IsDevelopment() ? frontendPath : frontendDistPath;

        Runtime = _nodeService.Platform.CreateThreadRuntime(baseDir, new NodeEmbeddingRuntimeSettings
        {
            // Initialize the require function so we can load modules
            MainScript = "globalThis.require = require('module').createRequire(process.execPath);\n"
        });

        _bundlePath = Path.Combine(baseDir, "entry-server.mjs");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Offload initialization so we don't block the host start
        _ = InitializeAsync();
        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitializeAsync()
    {
        try
        {
            await Runtime.RunAsync(async () =>
            {
                if (!_isDevelopment)
                {
                    var mod = await Runtime.ImportAsync(_bundlePath, null, true);
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

                if (_options.Value.Services.Injects.Count != 0)
                {
                    var projectAssembly = Assembly.GetEntryAssembly();

                    // 1. Find the Generated Module class
                    var moduleType = projectAssembly?.GetType("Microsoft.JavaScript.NodeApi.Generated.Module");

                    if (moduleType != null)
                    {
                        // 2. Find the Initialize method
                        var initMethod = moduleType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);

                        if (initMethod != null)
                        {
                            // 3. Invoke it
                            // This performs the registration of all the [JSExport] types automatically
                            var env = (JSRuntime.napi_env)JSValueScope.Current;
                            var exports = new JSObject();

                            // Invoke static method: Module.Initialize(env, exports)
                            initMethod.Invoke(null, [env, (JSRuntime.napi_value)(JSValue)exports]);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find the statically generated module for JS exports. " +
                                           "Ensure you have [JSExport] attributes in the interfaces registered for SSR injection." +
                                           "Proceeding with runtime registration. This will have a performance impact on startup.");

                        var exports = new JSObject();
                        var typeExporter = new TypeExporter(_nodeService.Marshaller, exports);

                        // Fallback: Try to register interfaces at runtime
                        foreach (var inject in _options.Value.Services.Injects)
                        {
                            var jsRef = typeExporter.ExportType(inject.InterfaceType);
                            var jsConstructor = jsRef.GetValue();

                            // 2. Patch the prototype to add camelCase aliases
                            if (jsConstructor.HasProperty("prototype"))
                            {
                                var prototype = jsConstructor["prototype"];
                                CreateCamelCaseAliases(prototype);
                            }

                            // 3. Patch static members (on the constructor itself)
                            CreateCamelCaseAliases(jsConstructor);
                        }
                    }
                }

                // Cache the Readable class for later use
                var readableModule = await Runtime.ImportAsync("stream", "Readable");
                NodeReadable = _nodeService.Marshaller.FromJS<INodeReadable>(readableModule);
            });
            _initializedTcs.TrySetResult();
        }
        catch (Exception ex)
        {
            _initializedTcs.TrySetException(ex);
        }
    }

    public async Task<JSReference> GetDevEntryServer()
    {
        await InitializationTask;
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
        await InitializationTask;
        JSValue entry;
        if (!_isDevelopment && EntryServer != null)
        {
            entry = EntryServer.GetValue();
        }
        else
        {
            var devEntryRef = await GetDevEntryServer();
            // We can dispose the reference immediately after getting a value in the current scope.
            // The value handle in the current scope will keep the object alive.
            using (devEntryRef)
            {
                entry = devEntryRef.GetValue();
            }
        }

        var entryFunctionOption = _options.Value.EntryFunction;
        var entryFunction = entry;

        foreach (var part in entryFunctionOption.Split('.'))
        {
            entryFunction = entryFunction[part];
        }

        return entryFunction;
    }

    private void CreateCamelCaseAliases(JSValue obj)
    {
        // Iterate over all keys (methods/properties)
        var properties = (JSArray)obj.GetPropertyNames(); // Returns JSArray
        foreach (JSValue key in properties)
        {
            if (!key.IsString()) continue;

            string pascalName = (string)key;
            if (string.IsNullOrEmpty(pascalName)) continue;

            // Convert to camelCase
            // (Simple logic: lowercase first char. Matches NodeApi generator logic)
            string camelName = char.ToLowerInvariant(pascalName[0]) + pascalName.Substring(1);

            // If names differ and the alias doesn't exist yet...
            if (pascalName != camelName && !obj.HasProperty(camelName))
            {
                // Create the alias pointing to the exact same value/function
                obj.SetProperty(camelName, obj.GetProperty(pascalName));
            }
        }
    }
}