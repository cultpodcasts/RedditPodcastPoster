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

    public async Task<string> FindPodcastId(Podcast podcast, IndexOptions indexOptions)
    {
        var match = await _spotifyItemResolver.FindPodcast(podcast.ToFindSpotifyPodcastRequest(), indexOptions);
        return match.Id;
    }

    public async Task<string> FindEpisodeId(Podcast podcast, Episode episode, IndexOptions indexOptions)
    {
        var match = await _spotifyItemResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(podcast, episode), indexOptions);
        if (match == null)
        {
            return string.Empty;
        }

        return match.Id ?? string.Empty;
    }
}