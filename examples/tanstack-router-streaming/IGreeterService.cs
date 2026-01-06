using Microsoft.JavaScript.NodeApi;

namespace tanstack_router_streaming;

[JSExport("greeterService")]
public interface IGreeterService
{
    /// <summary>
/// Generates a greeting message for the specified name.
/// </summary>
/// <param name="name">The name to include in the greeting.</param>
/// <returns>The greeting message.</returns>
public Task<string> GreetAsync(string name);
}