namespace tanstack_router_streaming;

public class GreeterService : IGreeterService
{
    /// <summary>
    /// Generates a greeting message for the specified name.
    /// </summary>
    /// <param name="name">The name to include in the greeting.</param>
    /// <returns>The greeting string in the format "Hello, {name}!".</returns>
    public async Task<string> GreetAsync(string name)
    {
        await Task.Delay(1000); // Simulate some async work
        return $"Hello, {name}!";
    }
}