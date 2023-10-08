using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyIdResolver : ISpotifyIdResolver
{
    private readonly ILogger<SpotifyIdResolver> _logger;
    private readonly ISpotifyItemResolver _spotifyItemResolver;

    public SpotifyIdResolver(
        ISpotifyItemResolver spotifyItemResolver,
        ILogger<SpotifyIdResolver> logger)
    {
        _spotifyItemResolver = spotifyItemResolver;
        _logger = logger;
    }

    public async Task<string> FindPodcastId(Podcast podcast, IndexingContext indexingContext)
    {
        var match = await _spotifyItemResolver.FindPodcast(podcast.ToFindSpotifyPodcastRequest(), indexingContext);
        return match?.Id ?? string.Empty;
    }

    public async Task<string> FindEpisodeId(Podcast podcast, Episode episode, IndexingContext indexingContext)
    {
        var match = await _spotifyItemResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(podcast, episode),
            indexingContext);

        return match?.Id ?? string.Empty;
    }
}