using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure;

public static class HostFactory
{
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
                .UseAzureMonitorExporter();
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
    /// Host and framework noise at Warning; application code at Information.
    /// Aligns with host.json and functions.bicep app settings.
    /// </summary>
    private static void ConfigureApplicationLogLevels(ILoggingBuilder logging)
    {
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("Function", LogLevel.Warning);
        logging.AddFilter("Host", LogLevel.Warning);
        logging.AddFilter("Azure", LogLevel.Warning);
        logging.AddFilter("RedditPodcastPoster", LogLevel.Information);
        logging.AddFilter("Indexer", LogLevel.Information);
        logging.AddFilter("Api", LogLevel.Information);
        logging.AddFilter("Discovery", LogLevel.Information);
    }
}
