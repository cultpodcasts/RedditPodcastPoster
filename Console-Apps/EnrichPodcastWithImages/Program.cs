using System.Diagnostics;
using System.Reflection;
using CommandLine;
using EnrichPodcastWithImages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddAppleServices()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddSpotifyServices()
    .AddBBCServices()
    .AddPodcastServices()
    .AddHttpClient();

builder.Services.AddPostingCriteria();
builder.Services.AddDelayedYouTubePublication();

using var host = builder.Build();

return await Parser.Default.ParseArguments<Request>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Request request)
{
    var podcastRepository = host.Services.GetService<IPodcastRepository>()!;
    var logger = host.Services.GetService<ILogger<Program>>()!;
    var imageUpdater = host.Services.GetService<IImageUpdater>()!;

    var podcastIds =
        await podcastRepository.GetAllBy(
                x => x.Name.ToLower().Contains(request.PodcastPartialMatch.ToLower()),
                p => new { id = p.Id })
            .ToListAsync();
    if (!podcastIds.Any())
    {
        logger.LogError("No podcasts found for partial-name '{podcastPartialName}'.", request.PodcastPartialMatch);
        return 0;
    }

    var indexingContext = new IndexingContext();
    foreach (var podcastId in podcastIds)
    {
        var updatedEpisodes = 0;

        var podcast = await podcastRepository.GetBy(x => x.Id == podcastId.id);
        if (podcast == null)
        {
            logger.LogError("No podcast with podcast-id '{podcastId}' found.", podcastId.id);
            continue;
        }

        logger.LogInformation("Enriching podcast '{podcastName}'.", podcast.Name);
        foreach (var episode in podcast.Episodes)
        {
            var imageUpdateRequest = (podcast, episode).ToEpisodeImageUpdateRequest();
            var updated = await imageUpdater.UpdateImages(podcast, episode, imageUpdateRequest, indexingContext);
            if (updated)
            {
                updatedEpisodes++;
            }
        }

        logger.LogInformation("Updated {updatedEpisodes} episodes.", updatedEpisodes);
        if (updatedEpisodes > 0)
        {
            await podcastRepository.Save(podcast);
        }
    }
    return 0;
}


string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}