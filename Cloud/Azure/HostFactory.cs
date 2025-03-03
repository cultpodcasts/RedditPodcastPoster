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
        var useAppInsights = builder.Configuration.UseApplicationInsightsConfiguration();
        builder.Services.AddLogging();
        if (useAppInsights)
        {
//            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging")); // ??? neccessary?
            builder.Services
                .AddApplicationInsightsTelemetryWorkerService()
                .ConfigureFunctionsApplicationInsights();
            builder.Logging.RemoveDefaultApplicationInsightsWarningRule();
        }

        configureServices(builder.Services);
#if DEBUG
        builder.Logging.ClearProviders();
#endif
        return builder.Build();
    }
}