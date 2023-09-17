using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public class PodcastServicesEpisodeEnricher : IPodcastServicesEpisodeEnricher
{
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly ILogger<PodcastServicesEpisodeEnricher> _logger;
    private readonly ISpotifyItemResolver _spotifyItemResolver;
    private readonly IYouTubeItemResolver _youTubeItemResolver;
    private readonly IApplePodcastEnricher _applePodcastEnricher;

    public PodcastServicesEpisodeEnricher(
        ISpotifyItemResolver spotifyItemResolver,
        IAppleEpisodeResolver appleEpisodeResolver,
        IYouTubeItemResolver youTubeItemResolver,
        IApplePodcastEnricher applePodcastEnricher,
        ILogger<PodcastServicesEpisodeEnricher> logger)
    {
        _spotifyItemResolver = spotifyItemResolver;
        _appleEpisodeResolver = appleEpisodeResolver;
        _youTubeItemResolver = youTubeItemResolver;
        _applePodcastEnricher = applePodcastEnricher;
        _logger = logger;
    }

    public async Task EnrichEpisodes(
        Podcast podcast,
        IList<Episode> newEpisodes,
        IndexOptions indexOptions
    )
    {
        _logger.LogInformation($"{nameof(EnrichEpisodes)} Enrich episodes with options {indexOptions}.");
        foreach (var episode in newEpisodes)
        {
            foreach (Service service in Enum.GetValues(typeof(Service)))
            {
                switch (service)
                {
                    case Service.Spotify
                        when episode.Urls.Spotify == null || string.IsNullOrWhiteSpace(episode.SpotifyId):
                        await EnrichFromSpotify(podcast, episode);
                        break;
                    case Service.Apple when episode.Urls.Apple == null || episode.AppleId == 0:
                        await EnrichFromApple(podcast, episode);
                        break;
                    case Service.YouTube when !indexOptions.SkipYouTubeUrlResolving && !string.IsNullOrWhiteSpace(
                                                  podcast.YouTubeChannelId) &&
                                              (episode.Urls.YouTube == null ||
                                               string.IsNullOrWhiteSpace(episode.YouTubeId)):
                        await EnrichFromYouTube(podcast, episode, indexOptions.ReleasedSince);
                        break;
                }
            }
        }
    }

    private async Task EnrichFromYouTube(Podcast podcast, Episode episode, DateTime? publishedSince)
    {
        if (podcast.IsDelayedYouTubePublishing(episode))
        {
            _logger.LogInformation($"{nameof(EnrichFromYouTube)} Bypassing enriching of '{episode.Title}' with release-date of '{episode.Release:R}' from YouTube as is below the {nameof(podcast.YouTubePublishingDelayTimeSpan)} which is '{podcast.YouTubePublishingDelayTimeSpan}'.");
            return;
        }

        var youTubeItem = await _youTubeItemResolver.FindEpisode(podcast, episode, publishedSince);
        if (!string.IsNullOrWhiteSpace(youTubeItem?.Id.VideoId))
        {
            _logger.LogInformation($"{nameof(EnrichFromApple)} Found matching YouTube episode: '{youTubeItem.Id.VideoId}' with title '{youTubeItem.Snippet.Title}' and release-date '{youTubeItem.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime:R}'.");
            episode.YouTubeId = youTubeItem.Id.VideoId;
            episode.Urls.YouTube = youTubeItem.ToYouTubeUrl();
        }
    }

    private async Task EnrichFromApple(Podcast podcast, Episode episode)
    {
        if (podcast.AppleId == null)
        {
            await _applePodcastEnricher.AddId(podcast);
        }

        if (podcast.AppleId != null)
        {
            var appleItem =
                await _appleEpisodeResolver.FindEpisode(FindAppleEpisodeRequestFactory.Create(podcast, episode));
            if (appleItem != null)
            {
                _logger.LogInformation($"{nameof(EnrichFromApple)} Found matching Apple episode: '{appleItem.Id}' with title '{appleItem.Title}' and release-date '{appleItem.Release:R}'.");
                episode.Urls.Apple = appleItem.Url.CleanAppleUrl();
                episode.AppleId = appleItem.Id;
            }
        }
    }

    private async Task EnrichFromSpotify(Podcast podcast, Episode episode)
    {
        var spotifyItem = await _spotifyItemResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(podcast, episode));
        if (spotifyItem?.FullEpisode != null)
        {
            _logger.LogInformation($"{nameof(EnrichFromSpotify)} Found matching Spotify episode: '{spotifyItem.FullEpisode.Id}' with title '{spotifyItem.FullEpisode.Name}' and release-date '{spotifyItem.FullEpisode.ReleaseDate}'.");
            episode.SpotifyId = spotifyItem.FullEpisode.Id;
            episode.Urls.Spotify = spotifyItem.Url();
        }
    }
}