namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// IMDb title page as the canonical film identity when several productions share a name.
/// Watch destinations stay on <see cref="IPlayable.Services"/> — IMDb is not a streaming service.
/// </summary>
public interface IFilmCanonical
{
    Uri? Imdb { get; set; }

    /// <summary>IMDb title id (<c>tt…</c>) for this film. The page is <see cref="Imdb"/>.</summary>
    string? ImdbId { get; set; }

    /// <summary>TMDB id for this film. Not a page URL. Films do not store a TheTVDB id.</summary>
    int? TmdbId { get; set; }
}
