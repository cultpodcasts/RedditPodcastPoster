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
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

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
    .AddInternetArchiveServices()
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
    var appleResolver = host.Services.GetService<IAppleEpisodeResolver>()!;
    var spotifyResolver = host.Services.GetService<ISpotifyEpisodeResolver>()!;
    var youTubeServiceFactory = host.Services.GetService<IYouTubeServiceFactory>()!;
    var youTubeVideoService = host.Services.GetService<IYouTubeVideoService>()!;
    var youTubeService = youTubeServiceFactory.Create(ApplicationUsage.Cli)!;
    var indexingContext = new IndexingContext();

    var podcastIds =
        await podcastRepository.GetAllBy(
                x => x.Name.ToLower().Contains(request.PodcastPartialMatch.ToLower()),
                p => new {id = p.Id})
            .ToListAsync();
    if (!podcastIds.Any())
    {
        logger.LogError("No podcasts found for partial-name '{podcastPartialName}'.", request.PodcastPartialMatch);
        return 0;
    }

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
            var updated = false;
            if (podcast.AppleId != null && episode.AppleId != null && episode.Images?.Apple == null)
            {
                var findAppleEpisodeRequest = FindAppleEpisodeRequestFactory.Create(podcast, episode);
                var appleItem = await appleResolver.FindEpisode(findAppleEpisodeRequest, indexingContext);
                if (appleItem != null && appleItem.Image != null)
                {
                    episode.Images ??= new EpisodeImages();
                    episode.Images.Apple = appleItem.Image;
                    updated = true;
                }
                else
                {
                    logger.LogError(
                        "Unable to obtain apple-item or image for apple-episode with apple-episode-id '{appleEpisodeId}'.",
                        episode.AppleId);
                }
            }

            if (!string.IsNullOrWhiteSpace(podcast.SpotifyId) && !string.IsNullOrWhiteSpace(episode.SpotifyId) &&
                episode.Images?.Spotify == null)
            {
                var findSpotifyEpisodeRequest = FindSpotifyEpisodeRequestFactory.Create(podcast, episode);
                try
                {
                    var spotifyEpisodeResponse =
                        await spotifyResolver.FindEpisode(findSpotifyEpisodeRequest, indexingContext);
                    if (spotifyEpisodeResponse.FullEpisode != null)
                    {
                        var image = spotifyEpisodeResponse.FullEpisode.GetBestImageUrl();
                        if (image != null)
                        {
                            episode.Images ??= new EpisodeImages();
                            episode.Images.Spotify = image;
                            updated = true;
                        }
                        else
                        {
                            logger.LogError(
                                "Unable to obtain image for spotify-episode with spotify-episode-id '{spotifyEpisodeId}'.",
                                episode.SpotifyId);
                        }
                    }
                    else
                    {
                        logger.LogError(
                            "Unable to obtain episode for spotify-episode with spotify-episode-id '{spotifyEpisodeId}'.",
                            episode.SpotifyId);
                    }
                }
                catch (EpisodeNotFoundException e)
                {
                    logger.LogError( "Failure retrieving spotify-episode with episode-id '{spotifyEpisodeId}'.",
                        episode.SpotifyId);
                }
            }


            if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) && !string.IsNullOrWhiteSpace(episode.YouTubeId) &&
                episode.Images?.YouTube == null)
            {
                var youTubeVideoResponse =
                    await youTubeVideoService.GetVideoContentDetails(youTubeService, [episode.YouTubeId],
                        indexingContext, true);
                var image = youTubeVideoResponse?.SingleOrDefault()?.GetImageUrl();

                if (image != null)
                {
                    episode.Images ??= new EpisodeImages();
                    episode.Images.YouTube = image;
                    updated = true;
                }
                else
                {
                    logger.LogError(
                        "Unable to obtain image for youtube-video with youtube-video-id '{spotifyEpisodeId}'.",
                        episode.YouTubeId);
                }
            }

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