using Azure;
using Indexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Persistence;

var host = Host.CreateDefaultBuilder(args)
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
    .ConfigureServices((hostBuilder, services) =>
    {
        services.Configure<CosmosDbSettings>(
            settings => hostBuilder.Configuration.GetSection("cosmosdb").Bind(settings));
        Ioc.ConfigureServices(hostBuilder, services);
    })
    .ConfigureLogging(logging => { logging.AllowAzureFunctionApplicationInsightsTraceLogging(); })
    .Build();

host.Run();