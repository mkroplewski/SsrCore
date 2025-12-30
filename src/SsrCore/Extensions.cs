using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SsrCore.Services;

namespace SsrCore;

/// <summary>
/// Provides extension methods for registering and using SsrCore in an ASP.NET Core application.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds SsrCore services to the dependency injection container.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder.</param>
    /// <param name="configureOptions">An action to configure SsrCore options.</param>
    public static void AddSsrCore(this WebApplicationBuilder builder, Action<SsrCoreOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        builder.Services.AddSingleton<NodeService>();
        builder.Services.AddScoped<RenderService>();
    }


    /// <summary>
    /// Configures the application to use SsrCore middleware.
    /// This serves static files from the 'wwwroot/client' directory and handles SSR routing.
    /// </summary>
    /// <param name="app">The WebApplication.</param>
    public static void UseSsrCore(this WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(app.Environment.WebRootPath, "client")),
        });

        app.MapFallback(
            async ([FromServices] RenderService renderService, HttpContext context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                
                await renderService.Render(context);
            });
    }
}