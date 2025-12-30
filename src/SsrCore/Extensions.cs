using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SsrCore.Services;

namespace SsrCore;

public static class Extensions
{
    public static void AddSsrCore(this WebApplicationBuilder builder, Action<SsrCoreOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        builder.Services.AddSingleton<NodeService>();
        builder.Services.AddScoped<RenderService>();
    }


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