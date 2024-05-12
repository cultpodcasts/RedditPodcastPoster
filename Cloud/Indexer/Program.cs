using Azure;
using Indexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.Services.ConfigureFunctionsApplicationInsights();
    })
    //.ConfigureFunctionsWebApplication(builder =>
    //{
    //    builder.UseFunctionsAuthorization();
    //})
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddEnvironmentVariables();
#if DEBUG
        builder.AddJsonFile("local.settings.json", false);
        builder.AddConfiguration(new ConfigurationBuilder().AddToConfigurationBuilder<Program>());
#endif
    })
    .ConfigureServices(Ioc.ConfigureServices)
    .ConfigureLogging(logging => { logging.AllowAzureFunctionApplicationInsightsTraceLogging(); })
    .Build();

host.Run();