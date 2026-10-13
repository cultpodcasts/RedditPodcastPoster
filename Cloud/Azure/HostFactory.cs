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
        ApplicationLogLevels.Configure(builder.Logging);

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

    private static void ConfigureAzureMonitorExporter(
        AzureMonitorExporterOptions options,
        IConfiguration configuration)
    {
        options.TracesPerSecond = null;
        options.SamplingRatio = ResolveTraceSamplingRatio(configuration);
        AzureMonitorExporterPolicy.Apply(options);
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
