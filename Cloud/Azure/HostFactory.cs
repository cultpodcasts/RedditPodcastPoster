using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Monitor.OpenTelemetry.Exporter;

namespace Azure;

public static class HostFactory
{
    private const float DefaultTraceSamplingRatio = 0.25f;

    public static IHost Create(string[] args, Action<IServiceCollection> configureServices)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        var isDevelopment = builder.Configuration.IsDevelopment();
        var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        var enableAzureMonitorExporter = !isDevelopment && !string.IsNullOrWhiteSpace(appInsightsConnectionString);

        builder.Services.AddLogging();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        ConfigureApplicationLogLevels(builder.Logging);

        builder.Logging.ConsoleWriteConfig();

        if (enableAzureMonitorExporter)
        {
            builder.Services
                .AddReducedAzureMonitorTelemetry()
                .AddOpenTelemetry()
                .UseFunctionsWorkerDefaults()
                .UseAzureMonitorExporter(options => ConfigureAzureMonitorExporter(options, builder.Configuration));
        }
        else
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
        }

        configureServices(builder.Services);
        return builder.Build();
    }

    /// <summary>
    /// Host/framework at Warning. Function app namespaces at Information.
    /// RedditPodcastPoster at Warning during cost review (high-volume pagination logs).
    /// </summary>
    private static void ConfigureApplicationLogLevels(ILoggingBuilder logging)
    {
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("Function", LogLevel.Warning);
        logging.AddFilter("Host", LogLevel.Warning);
        logging.AddFilter("Azure", LogLevel.Warning);
        logging.AddFilter("RedditPodcastPoster", LogLevel.Warning);
        logging.AddFilter("Indexer", LogLevel.Information);
        logging.AddFilter("Api", LogLevel.Information);
        logging.AddFilter("Discovery", LogLevel.Information);
    }

    private static void ConfigureAzureMonitorExporter(
        AzureMonitorExporterOptions options,
        IConfiguration configuration)
    {
        options.TracesPerSecond = null;
        options.SamplingRatio = ResolveTraceSamplingRatio(configuration);
        options.EnableTraceBasedLogsSampler = true;
    }

    private static float ResolveTraceSamplingRatio(IConfiguration configuration)
    {
        var percentage = configuration["APPLICATIONINSIGHTS_SAMPLING_PERCENTAGE"];
        if (float.TryParse(percentage, out var parsedPercentage) && parsedPercentage is > 0 and <= 100)
        {
            return parsedPercentage / 100f;
        }

        var ratio = configuration["OTEL_TRACES_SAMPLER_ARG"];
        if (float.TryParse(ratio, out var parsedRatio) && parsedRatio is > 0 and <= 1)
        {
            return parsedRatio;
        }

        return DefaultTraceSamplingRatio;
    }
}
