using RedditPodcastPoster.Models.TvShows;

namespace Api.Dtos.Extensions;

public static class TvShowExtension
{
    public static TvShowDto ToDto(this TvShow show) =>
        new TvShowDto
        {
            Imdb = show.Imdb,
            ImdbId = show.ImdbId,
            Tvdb = show.Tvdb,
            TvdbId = show.TvdbId,
            TmdbId = show.TmdbId
        }.WithPublisherFields(show);
}
