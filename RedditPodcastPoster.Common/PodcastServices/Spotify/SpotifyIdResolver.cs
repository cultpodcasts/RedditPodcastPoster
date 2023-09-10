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

    public async Task<string> FindPodcastId(Podcast podcast)
    {
        var match = await _spotifyItemResolver.FindPodcast(podcast.ToSpotifyFindPodcastRequest());
        return match.Id;
    }

    public async Task<string> FindEpisodeId(Podcast podcast, Episode episode)
    {
        var match = await _spotifyItemResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(podcast, episode));
        if (match == null)
        {
            return string.Empty;
        }

        return match.FullEpisode?.Id ?? string.Empty;
    }
}