using Prometheus;
using Serilog.Core;
using Serilog.Events;

namespace Mentalist.ReverseProxy.Metrics;

public class PrometheusEventSink : ILogEventSink
{
    private static readonly Counter LogLevelCounter = Prometheus
        .Metrics
        .CreateCounter("logger_events", "Total count of logger events for specific level", "level");

    public void Emit(LogEvent logEvent)
    {
        LogLevelCounter
            .Labels(logEvent.Level.ToString().ToLowerInvariant())
            .Inc();
    }
}