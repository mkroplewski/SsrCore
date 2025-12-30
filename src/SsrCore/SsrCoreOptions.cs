namespace SsrCore;


public enum RenderMode
{
    String,
    WebReadableStream,
    NodeReadableStream
}
public class SsrCoreOptions
{
    public RenderMode RenderMode { get; set; } = RenderMode.String;
}