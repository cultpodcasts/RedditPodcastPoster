using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;

namespace GuestHandleRecovery;

internal static class Program
{
    private static readonly Guid KarenMitchellEpisodeId = Guid.Parse("c9b116c6-e251-45f3-acfb-8a567428d047");

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
            .AddScoped<GuestHandleRecoveryProcessor>();

        using var host = builder.Build();
        return await Parser.Default.ParseArguments<GuestHandleRecoveryRequest>(args)
            .MapResult(async request =>
            {
                request.VerifyEpisodeId ??= KarenMitchellEpisodeId;
                var processor = host.Services.GetRequiredService<GuestHandleRecoveryProcessor>();
                await processor.Run(request);
                return 0;
            }, _ => Task.FromResult(-1));
    }
}
