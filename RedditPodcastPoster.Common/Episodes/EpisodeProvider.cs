using System.Text.RegularExpressions;
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

    public async Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        DateTime? processRequestReleasedSince,
        bool skipYouTube)
    {
        IList<Episode> episodes;
        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            episodes = await _spotifyEpisodeProvider.GetEpisodes(
                new SpotifyGetEpisodesRequest(podcast.SpotifyId, processRequestReleasedSince));
        }
        else if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            if (skipYouTube)
            {
                episodes = new List<Episode>();
            }
            else
            {
                episodes = await _youTubeEpisodeProvider.GetEpisodes(
                    new YouTubeGetEpisodesRequest(podcast.YouTubeChannelId, processRequestReleasedSince));
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Unable to handle podcast with id: {podcast.Id}, name: '{podcast.Name}'");
        }

        if (!podcast.IndexAllEpisodes && !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex,
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            episodes = episodes.Where(x => includeEpisodeRegex.IsMatch(x.Title)).ToList();
        }

        return episodes;
    }
}