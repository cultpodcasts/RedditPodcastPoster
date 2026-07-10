using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;

namespace EpisodeGuestHandleRestorer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();
        builder.Configuration
            .AddJsonFile("appsettings.json", true)
            .AddEnvironmentVariables("RedditPodcastPoster_")
            .AddCommandLine(args)
            .AddSecrets(Assembly.GetExecutingAssembly());

        builder.Services
            .AddLogging()
            .AddRepositories()
            .AddScoped<EpisodeGuestHandleRestorerProcessor>();

        using var host = builder.Build();
        return await Parser.Default.ParseArguments<EpisodeGuestHandleRestorerRequest>(args)
            .MapResult(async request =>
            {
                var processor = host.Services.GetRequiredService<EpisodeGuestHandleRestorerProcessor>();
                await processor.Run(request);
                return 0;
            }, _ => Task.FromResult(-1));
    }
}
