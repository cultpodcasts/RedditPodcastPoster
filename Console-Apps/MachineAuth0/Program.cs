using System.Reflection;
using MachineAuth0;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddSingleton<MachineAuth0Processor>()
    .AddScoped<IApiClient, ApiClient>()
    .AddScoped<IAuth0Client, Auth0Client>()
    .AddHttpClient<IApiClient, ApiClient>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        };
    });
builder.Services.BindConfiguration<Auth0Options>("auth0client");
builder.Services.BindConfiguration<ApiOptions>("api");

using var host = builder.Build();
var urlSubmitter = host.Services.GetService<MachineAuth0Processor>()!;
await urlSubmitter.Run();
return 0;