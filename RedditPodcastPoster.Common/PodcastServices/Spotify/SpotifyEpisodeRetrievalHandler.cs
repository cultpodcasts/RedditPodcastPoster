using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyEpisodeRetrievalHandler : ISpotifyEpisodeRetrievalHandler
{
    private readonly ILogger<SpotifyEpisodeRetrievalHandler> _logger;
    private readonly ISpotifyEpisodeProvider _spotifyEpisodeProvider;

    public SpotifyEpisodeRetrievalHandler(
        ISpotifyEpisodeProvider spotifyEpisodeProvider,
        ILogger<SpotifyEpisodeRetrievalHandler> logger)
    {
        _spotifyEpisodeProvider = spotifyEpisodeProvider;
        _logger = logger;
    }

    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        var handled = false;

        IList<Episode> episodes= new List<Episode>();
        if (podcast.ReleaseAuthority is null or Service.Spotify && !string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var getEpisodesResult =
                await _spotifyEpisodeProvider.GetEpisodes(new SpotifyPodcastId(podcast.SpotifyId), indexingContext);
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
        return new EpisodeRetrievalHandlerResponse(episodes, handled);
    }
}