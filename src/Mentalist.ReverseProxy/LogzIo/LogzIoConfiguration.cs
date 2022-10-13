namespace Mentalist.ReverseProxy.LogzIo;

public class LogzIoConfiguration
{
    public string? Url { get; set; }
    public string? BufferBaseFileName { get; set; }

    public bool BoostProperties { get; set; }
    public bool IncludeMessageTemplate { get; set; }
    public bool LowercaseLevel { get; set; }
    public bool UseElasticCommonScheme { get; set; }
}