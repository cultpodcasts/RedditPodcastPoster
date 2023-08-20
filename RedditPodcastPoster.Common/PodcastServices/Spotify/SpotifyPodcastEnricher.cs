using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyPodcastEnricher : ISpotifyPodcastEnricher
{
    private readonly ILogger<SpotifyPodcastEnricher> _logger;
    private readonly ISpotifyItemResolver _spotifyItemResolver;

    public SpotifyPodcastEnricher(
        ISpotifyItemResolver spotifyIdResolver,
        ILogger<SpotifyPodcastEnricher> logger)
    {
        _spotifyItemResolver = spotifyIdResolver;
        _logger = logger;
    }

    public async Task<bool> AddIdAndUrls(Podcast podcast)
    {
        var podcastShouldUpdate = false;
        if (string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var matchedPodcast = await _spotifyItemResolver.FindPodcast(podcast);
            if (!string.IsNullOrWhiteSpace(matchedPodcast.Id))
            {
                podcast.SpotifyId = matchedPodcast.Id;
                podcastShouldUpdate = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            foreach (var podcastEpisode in podcast.Episodes)
            {
                if (string.IsNullOrWhiteSpace(podcastEpisode.SpotifyId))
                {
                    var episode = await _spotifyItemResolver.FindEpisode(podcast, podcastEpisode);
                    if (!string.IsNullOrWhiteSpace(episode.Id))
                    {
                        podcastEpisode.SpotifyId = episode.Id;
                        podcastShouldUpdate = true;
                    }
                }
            }
        }

        return podcastShouldUpdate;
    }
}