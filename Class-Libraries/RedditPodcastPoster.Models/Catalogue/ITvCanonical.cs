namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// IMDb title and TheTVDB pages as canonical identity when several programmes or
/// episodes share a display name. Watch destinations stay on <see cref="IPlayable.Services"/> —
/// these URIs are not streaming services.
/// </summary>
public interface ITvCanonical
{
    Uri? Imdb { get; set; }

    /// <summary>IMDb title id (<c>tt…</c>). The page is <see cref="Imdb"/>.</summary>
    string? ImdbId { get; set; }

    Uri? Tvdb { get; set; }

    /// <summary>TheTVDB id for this show or episode. The page is <see cref="Tvdb"/>.</summary>
    long? TvdbId { get; set; }

    /// <summary>
    /// TMDB id for this document: the series id on a show, the episode id on an episode.
    /// An episode id does not by itself refresh TMDB; that call still needs the series id plus season and episode number.
    /// </summary>
    int? TmdbId { get; set; }
}
