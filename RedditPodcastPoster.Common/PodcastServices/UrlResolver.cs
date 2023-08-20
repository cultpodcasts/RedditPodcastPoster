using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public class UrlResolver : IUrlResolver
{
    private readonly IAppleUrlResolver _appleUrlResolver;
    private readonly ILogger<UrlResolver> _logger;
    private readonly ISpotifyUrlResolver _spotifyUrlResolver;
    private readonly IYouTubeUrlResolver _youTubeUrlResolver;

    public UrlResolver(
        ISpotifyUrlResolver spotifyUrlResolver,
        IAppleUrlResolver appleUrlResolver,
        IYouTubeUrlResolver youTubeUrlResolver,
        ILogger<UrlResolver> logger)
    {
        _spotifyUrlResolver = spotifyUrlResolver;
        _appleUrlResolver = appleUrlResolver;
        _youTubeUrlResolver = youTubeUrlResolver;
        _logger = logger;
    }

    public async Task<bool> ResolveEpisodeUrls(
        Podcast podcast, 
        IList<Episode> newEpisodes,
        DateTime? publishedSince,
        bool skipYouTubeUrlResolving
        )
    {
        var updateNeeded = false;
        foreach (var episode in newEpisodes)
        {
            foreach (Service service in Enum.GetValues(typeof(Service)))
            {
                updateNeeded |= service switch
                {
                    Service.Spotify when
                        episode.Urls.Spotify == null
                        => await EnrichSpotifyUrl(podcast, episode),
                    Service.Apple when
                        episode.Urls.Apple == null
                        => await EnrichAppleUrl(podcast, episode),
                    Service.YouTube when
                        !skipYouTubeUrlResolving && (episode.Urls.YouTube == null &&
                                                     !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
                        => await EnrichYouTubeUrl(podcast, episode, publishedSince),
                    _ => updateNeeded
                };
            }
        }

        return updateNeeded;
    }

    private async Task<bool> EnrichYouTubeUrl(Podcast podcast, Episode episode, DateTime? publishedSince)
    {
        var updateNeeded = false;
        episode.Urls.YouTube = await _youTubeUrlResolver.Resolve(podcast, episode, publishedSince);
        if (episode.Urls.YouTube != null)
        {
            updateNeeded = true;
        }

        return updateNeeded;
    }

    private async Task<bool> EnrichAppleUrl(Podcast podcast, Episode episode)
    {
        var updateNeeded = false;
        episode.Urls.Apple = await _appleUrlResolver.Resolve(podcast, episode);
        if (episode.Urls.Apple != null)
        {
            updateNeeded = true;
        }

        return updateNeeded;
    }

    private async Task<bool> EnrichSpotifyUrl(Podcast podcast, Episode episode)
    {
        var updateNeeded = false;
        episode.Urls.Spotify = await _spotifyUrlResolver.Resolve(podcast, episode);
        if (episode.Urls.Spotify != null)
        {
            updateNeeded = true;
        }

        return updateNeeded;
    }
}