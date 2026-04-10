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
        builder.Services.AddLogging();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

        builder.Logging.ConsoleWriteConfig();

        if (!isDevelopment)
        {
            builder.Services.AddOpenTelemetry()
                .UseFunctionsWorkerDefaults()
                .UseAzureMonitorExporter();
            //builder.Logging.RemoveInformationRules();
            //builder.Logging.SetMinimumLevel(LogLevel.Warning);
        }
        else
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
        }

        configureServices(builder.Services);
        return builder.Build();
    }
}