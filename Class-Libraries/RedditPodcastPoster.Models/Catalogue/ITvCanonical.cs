namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// IMDb title and TheTVDB pages as canonical identity when several programmes or
/// episodes share a display name. Watch destinations stay on <see cref="IPlayable.Services"/> —
/// these URIs are not streaming services.
/// </summary>
public interface ITvCanonical
{
    Uri? Imdb { get; set; }

    Uri? Tvdb { get; set; }

    /// <summary>TMDB id used to refresh <see cref="Imdb"/> and <see cref="Tvdb"/>. Not a page URL.</summary>
    int? TmdbId { get; set; }
}
