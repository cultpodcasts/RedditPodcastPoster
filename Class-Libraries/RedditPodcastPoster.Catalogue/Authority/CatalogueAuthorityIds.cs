using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// IMDb title id, TMDB id, and TheTVDB id for one film, show, or episode.
/// A film has no TheTVDB id. Page URLs are not part of this contract — callers generate them from ids when needed.
/// </summary>
public sealed record CatalogueAuthorityIds(
    string? ImdbId,
    int? TmdbId,
    long? TvdbId)
{
    public static CatalogueAuthorityIds Empty { get; } = new(null, null, null);

    public void Apply(Film film)
    {
        if (ImdbId is not null)
        {
            film.ImdbId = ImdbId;
        }

        if (TmdbId is not null)
        {
            film.TmdbId = TmdbId;
        }
    }

    public void Apply(TvShow show) => ApplyTv(show);

    public void Apply(TvShowEpisode episode) => ApplyTv(episode);

    /// <summary>
    /// Fills blank identity id fields only. Curator-set or previously stored ids are left alone.
    /// Does not write page URL members.
    /// </summary>
    public bool FillMissing(Film film)
    {
        var changed = false;
        if (ImdbId is not null && string.IsNullOrWhiteSpace(film.ImdbId))
        {
            film.ImdbId = ImdbId;
            changed = true;
        }

        if (TmdbId is not null && film.TmdbId is null)
        {
            film.TmdbId = TmdbId;
            changed = true;
        }

        return changed;
    }

    public bool FillMissing(ITvCanonical document) => FillMissingTv(document);

    private bool FillMissingTv(ITvCanonical document)
    {
        var changed = false;
        if (ImdbId is not null && string.IsNullOrWhiteSpace(document.ImdbId))
        {
            document.ImdbId = ImdbId;
            changed = true;
        }

        if (TmdbId is not null && document.TmdbId is null)
        {
            document.TmdbId = TmdbId;
            changed = true;
        }

        if (TvdbId is not null && document.TvdbId is null)
        {
            document.TvdbId = TvdbId;
            changed = true;
        }

        return changed;
    }

    private void ApplyTv(ITvCanonical document)
    {
        if (ImdbId is not null)
        {
            document.ImdbId = ImdbId;
        }

        if (TmdbId is not null)
        {
            document.TmdbId = TmdbId;
        }

        if (TvdbId is not null)
        {
            document.TvdbId = TvdbId;
        }
    }
}
