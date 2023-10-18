using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProvider : IEpisodeProvider
{
    private readonly IAppleEpisodeRetrievalHandler _appleEpisodeRetrievalHandler;
    private readonly IFoundEpisodeFilter _foundEpisodeFilter;
    private readonly ILogger<EpisodeProvider> _logger;
    private readonly ISpotifyEpisodeRetrievalHandler _spotifyEpisodeRetrievalHandler;
    private readonly IYouTubeEpisodeRetrievalHandler _youTubeEpisodeRetrievalHandler;

    public EpisodeProvider(
        IAppleEpisodeRetrievalHandler appleEpisodeRetrievalHandler,
        IYouTubeEpisodeRetrievalHandler youTubeEpisodeRetrievalHandler,
        ISpotifyEpisodeRetrievalHandler spotifyEpisodeRetrievalHandler,
        IFoundEpisodeFilter foundEpisodeFilter,
        ILogger<EpisodeProvider> logger)
    {
        _appleEpisodeRetrievalHandler = appleEpisodeRetrievalHandler;
        _youTubeEpisodeRetrievalHandler = youTubeEpisodeRetrievalHandler;
        _spotifyEpisodeRetrievalHandler = spotifyEpisodeRetrievalHandler;
        _foundEpisodeFilter = foundEpisodeFilter;
        _logger = logger;
    }

    public async Task<IList<Episode>> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        IList<Episode>? episodes = null;
        var handled = false;
        if (podcast.ReleaseAuthority is null or Service.Spotify)
        {
            (episodes, handled) = await _spotifyEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext);
            if (handled)
            {
                _logger.LogInformation(
                    $"Get Episodes for podcast '{podcast.Name}' handled by '{nameof(ISpotifyEpisodeRetrievalHandler)}'.");
            }
        }

        if (!handled && podcast.ReleaseAuthority != Service.YouTube)
        {
            (episodes, handled) = await _appleEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext);
            if (handled)
            {
                _logger.LogInformation(
                    $"Get Episodes for podcast '{podcast.Name}' handled by '{nameof(IAppleEpisodeRetrievalHandler)}'.");
            }
        }

        if (!handled || podcast.ReleaseAuthority is Service.YouTube)
        {
            (episodes, handled) = await _youTubeEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext);
            if (handled)
            {
                _logger.LogInformation(
                    $"Get Episodes for podcast '{podcast.Name}' handled by '{nameof(IYouTubeEpisodeRetrievalHandler)}'.");
            }
        }

        if (!handled)
        {
            _logger.LogInformation(
                $"Unable to handle podcast with name: '{podcast.Name}', id: {podcast.Id}. Spotify-Id: '{podcast.SpotifyId}', Apple-Id: '{podcast.AppleId}', YouTube-ChannelId: '{podcast.YouTubeChannelId}', YouTube-PlayListId: '{podcast.YouTubePlaylistId}'. Expensive-Queries? {nameof(podcast.HasExpensiveSpotifyEpisodesQuery)}= '{podcast.HasExpensiveSpotifyEpisodesQuery()}', {nameof(podcast.HasExpensiveYouTubePlaylistQuery)}= '{podcast.HasExpensiveYouTubePlaylistQuery()}'.");
        }

        if (episodes != null && episodes.Any() && !podcast.IndexAllEpisodes &&
            !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            episodes = _foundEpisodeFilter.ReduceEpisodes(podcast, episodes);
        }

        return episodes ?? new List<Episode>();
    }
}