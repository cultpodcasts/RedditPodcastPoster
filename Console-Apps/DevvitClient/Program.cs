using System.Reflection;
using CommandLine;
using DevvitClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Reddit;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddAuth0Client()
    .AddRepositories()
    .BindConfiguration<DevvitSettings>("devvit")
    .AddHttpClient<IDevvitClient, RedditPodcastPoster.Reddit.DevvitClient>();

builder.Services.AddSingleton<DevvitClientProcessor>();

using var host = builder.Build();
return await Parser.Default.ParseArguments<DevvitClientRequest>(args)
    .MapResult(async request => await Run(request), _ => Task.FromResult(-1));

async Task<int> Run(DevvitClientRequest request)
{
    var processor = host.Services.GetService<DevvitClientProcessor>()!;
    await processor.Run(request);
    return 0;
}
