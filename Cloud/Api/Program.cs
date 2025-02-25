using Api;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
#pragma warning disable AZFW0014
    .ConfigureFunctionsWorkerDefaults(builder =>
#pragma warning restore AZFW0014
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
        logging.AllowAzureFunctionApplicationInsightsTraceLogging();
    })
    .Build();

host.Run();