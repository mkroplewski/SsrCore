using Microsoft.JavaScript.NodeApi;

namespace SsrCore.Interfaces;

[JSImport]
public interface IEntryServer
{
    public Task<IJsWebResponse> Default(JSValue request);
    
}