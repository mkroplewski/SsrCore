using Microsoft.JavaScript.NodeApi;

namespace tanstack_router_streaming;

[JSExport("greeterService")]
public interface IGreeterService
{
    public Task<string> GreetAsync(string name);
}