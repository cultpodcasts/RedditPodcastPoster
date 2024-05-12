using Azure;
using Api;
using AzureFunctions.Extensions.OpenIDConnect.Isolated.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.Services.ConfigureFunctionsApplicationInsights();
        builder.UseAuthorization();
    })
    .ConfigureAppConfiguration(builder =>
    {
#if DEBUG
        builder.AddJsonFile("local.settings.json", false);
        builder.AddConfiguration(new ConfigurationBuilder().AddToConfigurationBuilder<Program>());
#endif
    })
    .ConfigureServices(Ioc.ConfigureServices)
    .ConfigureLogging(logging => { logging.AllowAzureFunctionApplicationInsightsTraceLogging(); })
    .Build();

host.Run();