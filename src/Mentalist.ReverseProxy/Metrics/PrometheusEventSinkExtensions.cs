using Serilog;
using Serilog.Configuration;

namespace Mentalist.ReverseProxy.Metrics;

public static class PrometheusEventSinkExtensions
{
    public static LoggerConfiguration Prometheus(this LoggerSinkConfiguration sinkConfiguration)
    {
        return sinkConfiguration.Sink(new PrometheusEventSink());
    }
}