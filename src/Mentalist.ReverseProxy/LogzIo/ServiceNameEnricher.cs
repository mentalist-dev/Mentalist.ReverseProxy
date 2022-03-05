using Serilog.Core;
using Serilog.Events;

namespace Mentalist.ReverseProxy.LogzIo;

public class ServiceNameEnricher : ILogEventEnricher
{
    private readonly string _serviceName;

    public ServiceNameEnricher(string serviceName)
    {
        _serviceName = serviceName;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!string.IsNullOrWhiteSpace(_serviceName))
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty("service", new ScalarValue(_serviceName)));
        }
    }
}