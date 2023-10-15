using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProvider : IEpisodeProvider
{
    private readonly IAppleEpisodeProvider _appleEpisodeProvider;
    private readonly ILogger<EpisodeProvider> _logger;
    private readonly ISpotifyEpisodeProvider _spotifyEpisodeProvider;
    private readonly IYouTubeEpisodeProvider _youTubeEpisodeProvider;

    public EpisodeProvider(
        ISpotifyEpisodeProvider spotifyEpisodeProvider,
        IAppleEpisodeProvider appleEpisodeProvider,
        IYouTubeEpisodeProvider youTubeEpisodeProvider,
        ILogger<EpisodeProvider> logger)
    {
        _spotifyEpisodeProvider = spotifyEpisodeProvider;
        _appleEpisodeProvider = appleEpisodeProvider;
        _youTubeEpisodeProvider = youTubeEpisodeProvider;
        _logger = logger;
    }

    public async Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        IndexingContext indexingContext)
    {
        IList<Episode> episodes = new List<Episode>();
        var handled = false;
        if (!indexingContext.SkipSpotifyUrlResolving && podcast.ReleaseAuthority is null or Service.Spotify &&
            !string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var getEpisodesResult = await _spotifyEpisodeProvider.GetEpisodes(new SpotifyPodcastId(podcast.SpotifyId), indexingContext);
            if (getEpisodesResult.Results != null && getEpisodesResult.Results.Any())
            {
                episodes = getEpisodesResult.Results;
            }

            if (getEpisodesResult.ExpensiveQueryFound)
            {
                podcast.SpotifyEpisodesQueryIsExpensive = true;
            }

            if (!indexingContext.SkipSpotifyUrlResolving)
            {
                handled = true;
            }
        }

        if (!handled && (
                podcast is {ReleaseAuthority: Service.Apple, AppleId: not null} ||
                (indexingContext.SkipSpotifyUrlResolving && podcast.AppleId != null &&
                 podcast.ReleaseAuthority != Service.YouTube)))
        {
            var foundEpisodes = await _appleEpisodeProvider.GetEpisodes(
                new ApplePodcastId(podcast.AppleId.Value), indexingContext);
            if (foundEpisodes != null)
            {
                episodes = foundEpisodes;
            }

            handled = true;
        }

        if (!handled && (podcast.ReleaseAuthority is Service.YouTube || !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId)))
        {
            if (!string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
            {
                if (podcast.HasExpensiveYouTubePlaylistQuery() && indexingContext.SkipExpensiveQueries)
                {
                    _logger.LogInformation($"Podcast '{podcast.Id}' has known expensive query and will not run this time.");
                    return new List<Episode>();
                }

                var getPlaylistEpisodesResult = await _youTubeEpisodeProvider.GetPlaylistEpisodes(
                    new YouTubePlaylistId(podcast.YouTubePlaylistId), indexingContext);
                if (getPlaylistEpisodesResult.Results != null)
                {
                    episodes = getPlaylistEpisodesResult.Results;
                }

                if (getPlaylistEpisodesResult.IsExpensiveQuery)
                {
                    podcast.YouTubePlaylistQueryIsExpensive = true;
                }
            }
            else
            {
                IEnumerable<string> knownIds;
                if (indexingContext.ReleasedSince.HasValue)
                {
                    knownIds = podcast.Episodes.Where(x => x.Release >= indexingContext.ReleasedSince)
                        .Select(x => x.YouTubeId);
                }
                else
                {
                    knownIds = podcast.Episodes.Select(x => x.YouTubeId);
                }

                var foundEpisodes = await _youTubeEpisodeProvider.GetEpisodes(
                    new YouTubeChannelId(podcast.YouTubeChannelId), indexingContext, knownIds);
                if (foundEpisodes != null)
                {
                    episodes = foundEpisodes;
                }
            }

            handled = true;
        }

        if (!handled)
        {
            throw new InvalidOperationException(
                $"Unable to handle podcast with id: {podcast.Id}, name: '{podcast.Name}'");
        }

        if (!podcast.IndexAllEpisodes && !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex,
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var eliminatedEpisodes = episodes.Where(x => !includeEpisodeRegex.IsMatch(x.Title));
            if (eliminatedEpisodes.Any())
            {
                _logger.LogInformation(
                    $"Eliminating episodes of podcast '{podcast.Name}' with id '{podcast.Id}' with titles '{string.Join(", ", eliminatedEpisodes.Select(x => x.Title))}' as they do not match {nameof(podcast.EpisodeIncludeTitleRegex)} of value '{podcast.EpisodeIncludeTitleRegex}'.");
            }

            episodes = episodes.Where(x => includeEpisodeRegex.IsMatch(x.Title)).ToList();
        }

        return episodes;
    }
}