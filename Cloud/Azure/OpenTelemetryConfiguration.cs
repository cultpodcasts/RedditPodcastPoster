using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Azure;

internal static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Runtime and host metrics that dominated AppMetrics ingestion (~57% of daily volume)
    /// without adding operational value at our scale.
    /// </summary>
    private static readonly string[] DroppedMetricPatterns =
    [
        "process.runtime.dotnet.*",
        "process.cpu.*",
        "kestrel.*",
        "http.server.active_requests",
        "azure.functions.health_check.reports",
        "_APPRESOURCEPREVIEW_*",
    ];

    public static IServiceCollection AddReducedAzureMonitorTelemetry(this IServiceCollection services)
    {
        services.ConfigureOpenTelemetryMeterProvider(metrics =>
        {
            foreach (var pattern in DroppedMetricPatterns)
            {
                metrics.AddView(instrumentName: pattern, MetricStreamConfiguration.Drop);
            }
        });

        return services;
    }
}
