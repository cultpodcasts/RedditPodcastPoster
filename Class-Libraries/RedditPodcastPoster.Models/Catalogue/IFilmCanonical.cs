namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// IMDb title page as the canonical film identity when several productions share a name.
/// Watch destinations stay on <see cref="IPlayable.Services"/> — IMDb is not a streaming service.
/// </summary>
public interface IFilmCanonical
{
    Uri? Imdb { get; set; }

    /// <summary>TMDB movie id used to refresh <see cref="Imdb"/>. Not a page URL.</summary>
    int? TmdbId { get; set; }
}
