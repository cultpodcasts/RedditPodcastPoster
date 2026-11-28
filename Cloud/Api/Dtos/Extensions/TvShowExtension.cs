using RedditPodcastPoster.Models.TvShows;

namespace Api.Dtos.Extensions;

public static class TvShowExtension
{
    public static TvShowDto ToDto(this TvShow show) =>
        new TvShowDto
        {
            ImdbId = show.ImdbId,
            TvdbId = show.TvdbId,
            TmdbId = show.TmdbId,
            Trailers = show.Trailers is { Count: > 0 } trailers
                ? trailers.Select(trailer => new TvShowTrailerDto
                {
                    Id = trailer.Id,
                    Title = trailer.Title,
                    Url = trailer.Url,
                    Image = trailer.Image,
                    Release = trailer.Release,
                    Duration = CatalogueDtoMapping.FormatDuration(trailer.Length)
                }).ToArray()
                : null
        }.WithPublisherFields(show).WithServiceIds(show.Indexing);
}
