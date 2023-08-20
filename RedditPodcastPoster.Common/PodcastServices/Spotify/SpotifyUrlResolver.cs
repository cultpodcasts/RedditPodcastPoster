using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyUrlResolver : ISpotifyUrlResolver
{
    private readonly ILogger<SpotifyUrlResolver> _logger;
    private readonly ISpotifyItemResolver _spotifyItemResolver;

    public SpotifyUrlResolver(ISpotifyItemResolver spotifyItemResolver, ILogger<SpotifyUrlResolver> logger)
    {
        _spotifyItemResolver = spotifyItemResolver;
        _logger = logger;
    }

    public async Task<Uri?> Resolve(Podcast podcast, Episode episode)
    {
        var match = await _spotifyItemResolver.FindEpisode(podcast, episode);
        return match.Url();
    }
}