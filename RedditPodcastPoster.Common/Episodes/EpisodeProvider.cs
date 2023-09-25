using System.ComponentModel.Design;
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
        if (podcast.ReleaseAuthority is null or Service.Spotify && !string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            episodes = await _spotifyEpisodeProvider.GetEpisodes(
                new SpotifyGetEpisodesRequest(podcast.SpotifyId, processRequestReleasedSince));
            _logger.LogInformation($"{nameof(GetEpisodes)} (Spotify) - Found '{episodes.Count}' episodes released since {processRequestReleasedSince:R}");
        }
        else if (podcast.ReleaseAuthority is Service.YouTube || !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            if (skipYouTube)
            {
                _logger.LogInformation(
                    $"{nameof(GetEpisodes)} Bypassing resolving using YouTube due to '{nameof(skipYouTube)}'.");
                episodes = new List<Episode>();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
                {
                    episodes = await _youTubeEpisodeProvider.GetPlaylistEpisodes(
                        new YouTubeGetPlaylistEpisodesRequest(podcast.YouTubePlaylistId, processRequestReleasedSince));

                }
                else
                {
                    episodes = await _youTubeEpisodeProvider.GetEpisodes(
                        new YouTubeGetEpisodesRequest(podcast.YouTubeChannelId, processRequestReleasedSince));
                    _logger.LogInformation($"{nameof(GetEpisodes)} (YouTube) - Found '{episodes.Count}' episodes released since {processRequestReleasedSince:R}");
                }
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Unable to handle podcast with id: {podcast.Id}, name: '{podcast.Name}'");
        }

        if (!podcast.IndexAllEpisodes && !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            _logger.LogInformation($"{nameof(GetEpisodes)} - Filtering episodes by '{nameof(podcast.EpisodeIncludeTitleRegex)}'='{podcast.EpisodeIncludeTitleRegex}'.");
            var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex,
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            episodes = episodes.Where(x => includeEpisodeRegex.IsMatch(x.Title)).ToList();
        }

        return episodes;
    }
}