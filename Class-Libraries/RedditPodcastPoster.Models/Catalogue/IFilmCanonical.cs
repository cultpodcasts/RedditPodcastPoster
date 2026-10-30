namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// IMDb title id (and TMDB id) as the canonical film identity when several productions share a name.
/// Watch destinations stay on <see cref="IPlayable.Services"/> — IMDb is not a streaming service.
/// Page URLs are derived from <see cref="ImdbId"/> when a link is needed.
/// </summary>
public interface IFilmCanonical
{
    /// <summary>IMDb title id (<c>tt…</c>) for this film.</summary>
    string? ImdbId { get; set; }

    /// <summary>TMDB id for this film. Films do not store a TheTVDB id.</summary>
    int? TmdbId { get; set; }
}
