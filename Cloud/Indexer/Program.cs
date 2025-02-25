using Azure;
using Indexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.Services.ConfigureFunctionsApplicationInsights();
    })
    .ConfigureAppConfiguration(builder =>
    {
#if DEBUG
        builder.AddJsonFile("local.settings.json", false);
        builder.AddConfiguration(new ConfigurationBuilder().AddToConfigurationBuilder<Program>());
#endif
    })
    .ConfigureServices(Ioc.ConfigureServices)
    .ConfigureLogging(logging =>
    {
#if DEBUG
        logging.ClearProviders();
#endif
        logging.AllowAzureFunctionApplicationInsightsTraceLogging();
    })
    .Build();

host.Run();