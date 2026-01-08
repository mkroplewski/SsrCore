using Microsoft.JavaScript.NodeApi;

namespace react_router_streaming;

[JSExport("greeterService")]
public interface IGreeterService
{
    public Task<string> GreetAsync(string name);
}