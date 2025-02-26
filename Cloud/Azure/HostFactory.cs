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
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureAppConfiguration(builder =>
            {
#if DEBUG
                builder.AddJsonFile("local.settings.json", false);
                builder.AddConfiguration(config);
#endif
            })
            .ConfigureServices(services=>
            {
                services
                    .AddLogging()
                    //.AddApplicationInsightsTelemetryWorkerService()
                    //.ConfigureFunctionsApplicationInsights()
                    ;
                configureServices(services);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                //        logging.AllowAzureFunctionApplicationInsightsTraceLogging();
            })
            .Build();
        return host;
    }

}