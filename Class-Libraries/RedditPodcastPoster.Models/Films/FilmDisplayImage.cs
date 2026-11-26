namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// Display / search image for a film when streaming services have none (RPP #1422): identity image first, else
/// the first trailer thumbnail (create-from-trailer stores the YouTube art only on <c>trailers[].image</c>).
/// </summary>
public static class FilmDisplayImage
{
    /// <summary>
    /// Identity (TMDB) image when set; otherwise the first trailer that has an image. Null when neither exists.
    /// Callers that already prefer a streaming-service image should only use this after that check fails.
    /// </summary>
    public static Uri? PreferIdentityOrTrailer(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        if (film.Image is not null)
        {
            return film.Image;
        }

        return film.Trailers?
            .Select(trailer => trailer.Image)
            .FirstOrDefault(image => image is not null);
    }
}
