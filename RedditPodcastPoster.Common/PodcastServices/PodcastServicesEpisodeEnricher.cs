using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public class PodcastServicesEpisodeEnricher : IPodcastServicesEpisodeEnricher
{
    private readonly IAppleItemResolver _appleItemResolver;
    private readonly ILogger<PodcastServicesEpisodeEnricher> _logger;
    private readonly ISpotifyItemResolver _spotifyItemResolver;
    private readonly IYouTubeItemResolver _youTubeItemResolver;

    public PodcastServicesEpisodeEnricher(
        ISpotifyItemResolver spotifyItemResolver,
        IAppleItemResolver appleItemResolver,
        IYouTubeItemResolver youTubeItemResolver,
        ILogger<PodcastServicesEpisodeEnricher> logger)
    {
        _spotifyItemResolver = spotifyItemResolver;
        _appleItemResolver = appleItemResolver;
        _youTubeItemResolver = youTubeItemResolver;
        _logger = logger;
    }

    public async Task EnrichEpisodes(
        Podcast podcast,
        IList<Episode> newEpisodes,
        DateTime? publishedSince,
        bool skipYouTubeUrlResolving
    )
    {
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
                    case Service.YouTube when !skipYouTubeUrlResolving && !string.IsNullOrWhiteSpace(
                                                  podcast.YouTubeChannelId) &&
                                              (episode.Urls.YouTube == null ||
                                               string.IsNullOrWhiteSpace(episode.YouTubeId)):
                        await EnrichFromYouTube(podcast, episode, publishedSince);
                        break;
                }
            }
        }
    }

    private async Task EnrichFromYouTube(Podcast podcast, Episode episode, DateTime? publishedSince)
    {
        var youTubeItem = await _youTubeItemResolver.FindEpisode(podcast, episode, publishedSince);
        if (!string.IsNullOrWhiteSpace(youTubeItem.Id.VideoId))
        {
            episode.YouTubeId = youTubeItem.Id.VideoId;
            episode.Urls.YouTube = youTubeItem.ToYouTubeUrl();
        }
    }

    private async Task EnrichFromApple(Podcast podcast, Episode episode)
    {
        var appleItem = await _appleItemResolver.FindEpisode(podcast, episode);
        if (appleItem != null)
        {
            episode.Urls.Apple = appleItem.Url;
            episode.AppleId = appleItem.Id;
        }
    }

    private async Task EnrichFromSpotify(Podcast podcast, Episode episode)
    {
        var spotifyItem = await _spotifyItemResolver.FindEpisode(podcast, episode);
        if (spotifyItem?.FullEpisode != null)
        {
            episode.SpotifyId = spotifyItem.FullEpisode.Id;
            episode.Urls.Spotify = spotifyItem.Url();
        }
    }
}