using Microsoft.JavaScript.NodeApi;

namespace SsrCore.Interfaces;

[JSImport]
public interface INodeReadable
{
    [JSExport("fromWeb")]
    Stream FromWeb(JSValue webReadableStream);
}