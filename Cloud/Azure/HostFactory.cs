using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure;

public static class HostFactory
{
    public static IHost Create<T>(string[] args, Action<IServiceCollection> configureServices) where T : class
    {
        var builder = FunctionsApplication.CreateBuilder(args);
#if DEBUG
        builder.Configuration.AddLocalConfiguration<T>();
#endif
        builder.Services.AddLogging();
        builder.Services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
        configureServices(builder.Services);
#if DEBUG
        builder.Logging.ClearProviders();
#endif
//        builder.Logging.SetApplicationInsightsBaselineWarningRule(LogLevel.Information);
        return builder.Build();
    }
}