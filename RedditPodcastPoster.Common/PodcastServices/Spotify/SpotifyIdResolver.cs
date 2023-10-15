using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyIdResolver : ISpotifyIdResolver
{
    private readonly ILogger<SpotifyIdResolver> _logger;
    private readonly ISpotifyEpisodeResolver _spotifyEpisodeResolver;
    private readonly ISpotifyPodcastResolver _spotifyPodcastResolver;

    public SpotifyIdResolver(
        ISpotifyEpisodeResolver spotifyEpisodeResolver,
        ISpotifyPodcastResolver spotifyPodcastResolver,
        ILogger<SpotifyIdResolver> logger)
    {
        _spotifyEpisodeResolver = spotifyEpisodeResolver;
        _spotifyPodcastResolver = spotifyPodcastResolver;
        _logger = logger;
    }

    public async Task<string> FindPodcastId(Podcast podcast, IndexingContext indexingContext)
    {
        var match = await _spotifyPodcastResolver.FindPodcast(podcast.ToFindSpotifyPodcastRequest(), indexingContext);
        return match?.Id ?? string.Empty;
    }

    public async Task<string> FindEpisodeId(Podcast podcast, Episode episode, IndexingContext indexingContext)
    {
        var match = await _spotifyEpisodeResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(podcast, episode),
            indexingContext);

        return match?.Id ?? string.Empty;
    }
}