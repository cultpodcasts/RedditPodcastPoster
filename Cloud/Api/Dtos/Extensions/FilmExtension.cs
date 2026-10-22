using Api.Dtos.Mapping;
using RedditPodcastPoster.Models.Films;

namespace Api.Dtos.Extensions;

public static class FilmExtension
{
    public static FilmDto ToDto(this Film film) =>
        new FilmDto
        {
            Imdb = film.Imdb,
            ImdbId = film.ImdbId,
            TmdbId = film.TmdbId,
            Release = CatalogueDtoMapping.FormatRelease(film.Release),
            Duration = CatalogueDtoMapping.FormatDuration(film.Length),
            Explicit = film.Explicit,
            Ignored = film.Ignored,
            Posted = film.Posted,
            Tweeted = film.Tweeted,
            BlueskyPosted = film.BlueskyPosted,
            Subjects = [.. film.Subjects],
            Guests = film.Guests,
            Services = film.Services.ToDtos(),
            Trailers = film.Trailers is { Count: > 0 } trailers
                ? trailers.Select(trailer => new FilmTrailerDto
                {
                    Id = trailer.Id,
                    Title = trailer.Title,
                    Url = trailer.Url,
                    Image = trailer.Image,
                    Release = trailer.Release,
                    Duration = CatalogueDtoMapping.FormatDuration(trailer.Length)
                }).ToArray()
                : null
        }.WithPublisherFields(film);
}
