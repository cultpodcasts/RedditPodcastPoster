using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure;

public static class HostFactory
{

    public static IHost Create<T>(Action<IServiceCollection> configureServices) where T : class
    {
        var config = new ConfigurationBuilder().AddConfiguration<T>();
        var builder = FunctionsApplication.CreateBuilder([]);
#if DEBUG
        builder.Configuration.AddJsonFile("local.settings.json", false);
        builder.Configuration.AddConfiguration(config);
#endif
        builder.Services.AddLogging();
        //builder.Services
        //    .AddApplicationInsightsTelemetryWorkerService()
        //    .ConfigureFunctionsApplicationInsights();

        configureServices(builder.Services);
        builder.Logging.ClearProviders();
        //builder.Logging.AllowAzureFunctionApplicationInsightsTraceLogging();
        return builder.Build();
    }
}