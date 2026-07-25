using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

public class SpotifyEpisodePaginatorFactory(
    ILogger<SimpleEpisodePaginator> simpleEpisodePaginatorLogger,
    ILogger<AscendingEpisodePaginator> ascendingEpisodePaginatorLogger) : ISpotifyEpisodePaginatorFactory
{
    public IPaginator CreateReverseChronologicalPaginator(DateTime? releasedSince) =>
        new SimpleEpisodePaginator(releasedSince, isInReverseOrder: true, simpleEpisodePaginatorLogger);

    public IPaginator CreateAscendingEndJumpPaginator(DateTime releasedSince) =>
        new AscendingEpisodePaginator(
            releasedSince,
            ascendingEpisodePaginatorLogger,
            new SimpleEpisodePaginator(releasedSince, isInReverseOrder: false, simpleEpisodePaginatorLogger));
}
