using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProvider : IEpisodeProvider
{
    private readonly ILogger<EpisodeProvider> _logger;
    private readonly ISpotifyEpisodeProvider _spotifyEpisodeProvider;
    private readonly IYouTubeEpisodeProvider _youTubeEpisodeProvider;

    public EpisodeProvider(
        ISpotifyEpisodeProvider spotifyEpisodeProvider,
        IYouTubeEpisodeProvider youTubeEpisodeProvider,
        ILogger<EpisodeProvider> logger)
    {
        _spotifyEpisodeProvider = spotifyEpisodeProvider;
        _youTubeEpisodeProvider = youTubeEpisodeProvider;
        _logger = logger;
    }

    public async Task<IList<Episode>?> GetEpisodes(
        Podcast podcast, 
        DateTime? processRequestReleasedSince,
        bool skipYouTube)
    {
        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            return await _spotifyEpisodeProvider.GetEpisodes(podcast, processRequestReleasedSince);
        }

        if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            if (skipYouTube)
            {
                return new List<Episode>();
            }

            return await _youTubeEpisodeProvider.GetEpisodes(podcast, processRequestReleasedSince);
        }

        throw new InvalidOperationException($"Unable to handle podcast with id: {podcast.Id}, name: '{podcast.Name}'");
    }
}