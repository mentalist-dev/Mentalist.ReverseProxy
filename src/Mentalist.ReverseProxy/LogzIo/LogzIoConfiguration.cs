namespace Mentalist.ReverseProxy.LogzIo;

public class LogzIoConfiguration
{
    public string? Url { get; set; }
    public string? BufferPathFormat { get; set; }

    public bool BoostProperties { get; set; }
    public bool IncludeMessageTemplate { get; set; }
    public bool LowercaseLevel { get; set; }
}