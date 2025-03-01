using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure;

public static class HostFactory
{
    public static IHost Create(string[] args, Action<IServiceCollection> configureServices)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
#if DEBUG
        builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());
#endif
        var useAppInsights = builder.Configuration.UseApplicationInsightsConfiguration();
        Action<ILoggingBuilder> loggingBuilderAction =
            useAppInsights ? x => x.SetDefaultApplicationInsightsWarningRule() : x => { };
        builder.Services.AddLogging(loggingBuilderAction);
        if (useAppInsights)
        {
            builder.Services
                .AddApplicationInsightsTelemetryWorkerService()
                .ConfigureFunctionsApplicationInsights();
        }

        configureServices(builder.Services);
#if DEBUG
        builder.Logging.ClearProviders();
#endif
        return builder.Build();
    }
}