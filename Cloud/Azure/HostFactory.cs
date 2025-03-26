using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
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
        if (!isDevelopment)
        {
            builder.Services
                .AddApplicationInsightsTelemetryWorkerService()
                .ConfigureFunctionsApplicationInsights();
            builder.Logging.RemoveDefaultApplicationInsightsWarningRule();
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