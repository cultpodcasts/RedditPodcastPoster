using System.Reflection;
using MachineAuth0;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EdgeApi.Extensions;

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
    .AddAuth0Client()
    .AddEdgeApiClient(true);

using var host = builder.Build();
var urlSubmitter = host.Services.GetService<MachineAuth0Processor>()!;
await urlSubmitter.Run();
return 0;