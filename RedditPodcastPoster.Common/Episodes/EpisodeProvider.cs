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
        IndexOptions indexOptions)
    {
        IList<Episode> episodes = new List<Episode>();
        if (podcast.ReleaseAuthority is null or Service.Spotify && !string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var foundEpisodes = await _spotifyEpisodeProvider.GetEpisodes(
                new SpotifyPodcastId(podcast.SpotifyId), indexOptions);
            if (foundEpisodes != null)
            {
                episodes = foundEpisodes;
                _logger.LogInformation(
                    $"{nameof(GetEpisodes)} (Spotify) - Found '{episodes.Count}' episodes released since {indexOptions.ReleasedSince:R}");
            }
        }
        else if (podcast.ReleaseAuthority is Service.YouTube || !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            if (!string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
            {
                var foundEpisodes = await _youTubeEpisodeProvider.GetPlaylistEpisodes(
                    new YouTubePlaylistId(podcast.YouTubePlaylistId), indexOptions);
                if (foundEpisodes != null)
                {
                    episodes = foundEpisodes;
                }
            }
            else
            {
                var foundEpisodes = await _youTubeEpisodeProvider.GetEpisodes(
                    new YouTubeChannelId(podcast.YouTubeChannelId), indexOptions);
                if (foundEpisodes != null)
                {
                    episodes = foundEpisodes;
                    _logger.LogInformation(
                        $"{nameof(GetEpisodes)} (YouTube) - Found '{episodes.Count}' episodes released since {indexOptions.ReleasedSince:R}");
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
            _logger.LogInformation(
                $"{nameof(GetEpisodes)} - Filtering episodes by '{nameof(podcast.EpisodeIncludeTitleRegex)}'='{podcast.EpisodeIncludeTitleRegex}'.");
            var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex,
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            episodes = episodes.Where(x => includeEpisodeRegex.IsMatch(x.Title)).ToList();
        }

        return episodes;
    }
}