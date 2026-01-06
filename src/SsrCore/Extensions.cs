using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
        builder.Services.AddSingleton<SsrContextService>();

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddHttpForwarder();
            builder.Services.AddSingleton<ViteProxyService>();
        }
        builder.Services.AddScoped<RenderService>();

    }


    /// <summary>
    /// Configures the application to use SsrCore middleware.
    /// This serves static files from the 'wwwroot/client' directory and handles SSR routing.
    /// </summary>
    /// <param name="app">The WebApplication.</param>
    public static void UseSsrCore(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(app.Environment.WebRootPath, "client")),
            });
        }

        if (app.Environment.IsDevelopment())
        {
            app.Use(async (context, next) =>
            {
                var viteService = context.RequestServices.GetRequiredService<ViteProxyService>();
                var handled = await viteService.ForwardRequest(context);

                // If the response has not been handled, continue to the next middleware
                if (!handled && !context.Response.HasStarted)
                {
                    await next();
                }
            });
        }

        app.Use(async (HttpContext context, RequestDelegate next) =>
        {
            var renderService = context.RequestServices.GetRequiredService<RenderService>();
            await renderService.Render(context);
        });




    }
}