namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// IMDb title id and TheTVDB id as canonical identity when several programmes or
/// episodes share a display name. Watch destinations stay on <see cref="IPlayable.Services"/> —
/// these ids are not streaming services. Page URLs are derived from the ids when a link is needed.
/// </summary>
public interface ITvCanonical
{
    /// <summary>IMDb title id (<c>tt…</c>).</summary>
    string? ImdbId { get; set; }

    /// <summary>TheTVDB id for this show or episode.</summary>
    long? TvdbId { get; set; }

    /// <summary>
    /// TMDB id for this document: the series id on a show, the episode id on an episode.
    /// An episode id does not by itself refresh TMDB; that call still needs the series id plus season and episode number.
    /// </summary>
    int? TmdbId { get; set; }
}
