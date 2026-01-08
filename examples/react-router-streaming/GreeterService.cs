namespace react_router_streaming;

public class GreeterService : IGreeterService
{
    public async Task<string> GreetAsync(string name)
    {
        await Task.Delay(1000); // Simulate some async work
        return $"Hello, {name}!";
    }
}