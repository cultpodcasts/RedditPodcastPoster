using RedditPodcastPoster.Models.Films;

namespace Api.Dtos.Extensions;

public static class FilmExtension
{
    public static FilmDto ToDto(this Film film)
    {
        return new FilmDto
        {
            Id = film.Id,
            Name = film.Name,
            Imdb = film.Imdb,
            TmdbId = film.TmdbId
        };
    }
}
