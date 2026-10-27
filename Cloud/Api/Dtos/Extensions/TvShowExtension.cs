using RedditPodcastPoster.Models.TvShows;

namespace Api.Dtos.Extensions;

public static class TvShowExtension
{
    public static TvShowDto ToDto(this TvShow show) =>
        new TvShowDto
        {
            ImdbId = show.ImdbId,
            TvdbId = show.TvdbId,
            TmdbId = show.TmdbId
        }.WithPublisherFields(show).WithServiceIds(show, show.Indexing);
}
