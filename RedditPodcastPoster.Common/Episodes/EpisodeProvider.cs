using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

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

    public async Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        IndexingContext indexingContext)
    {
        IList<Episode> episodes = new List<Episode>();
        var handled = false;
        if (!indexingContext.SkipSpotifyUrlResolving && podcast.ReleaseAuthority is null or Service.Spotify &&
            !string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            (episodes, handled) = await _spotifyEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext, episodes);
        }

        if (!handled && (
                podcast is {ReleaseAuthority: Service.Apple, AppleId: not null} ||
                (indexingContext.SkipSpotifyUrlResolving && podcast.AppleId != null &&
                 podcast.ReleaseAuthority != Service.YouTube)))
        {
            var response = await _appleEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext, episodes);
            handled = response.Handled;
            episodes = response.Episodes;
        }

        if (!handled && (podcast.ReleaseAuthority is Service.YouTube ||
                         !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId)))
        {
            var response = await _youTubeEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext, episodes);
            episodes = response.Episodes;
            handled = response.Handled;
        }

        if (!handled)
        {
            throw new InvalidOperationException(
                $"Unable to handle podcast with id: {podcast.Id}, name: '{podcast.Name}'");
        }

        if (!podcast.IndexAllEpisodes && !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            episodes = _foundEpisodeFilter.ReduceEpisodes(podcast, episodes);
        }

        return episodes;
    }
}