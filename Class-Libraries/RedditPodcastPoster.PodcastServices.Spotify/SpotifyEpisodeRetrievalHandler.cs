using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyEpisodeRetrievalHandler(
    ISpotifyEpisodeProvider spotifyEpisodeProvider,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SpotifyEpisodeRetrievalHandler> logger
#pragma warning restore CS9113 // Parameter is unread.
) : ISpotifyEpisodeRetrievalHandler
{
    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        var handled = false;

        IList<Episode> episodes = new List<Episode>();
        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var getEpisodesRequest = new GetEpisodesRequest(new SpotifyPodcastId(podcast.SpotifyId),
                podcast.SpotifyMarket,
                podcast.HasExpensiveSpotifyEpisodesQuery());
            var getEpisodesResult = await spotifyEpisodeProvider.GetEpisodes(getEpisodesRequest, indexingContext);
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