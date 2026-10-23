using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// IMDb title id, TMDB id, and TheTVDB id collected for one film, show, or episode.
/// A film has no TheTVDB id. Page URLs are the public links for the ids we hold.
/// </summary>
public sealed record CatalogueAuthorityIds(
    string? ImdbId,
    int? TmdbId,
    long? TvdbId,
    Uri? Imdb,
    Uri? Tvdb)
{
    public static CatalogueAuthorityIds Empty { get; } = new(null, null, null, null, null);

    public void Apply(Film film)
    {
        if (ImdbId is not null)
        {
            film.ImdbId = ImdbId;
            film.Imdb = Imdb;
        }

        if (TmdbId is not null)
        {
            film.TmdbId = TmdbId;
        }
    }

    public void Apply(TvShow show) => ApplyTv(show);

    public void Apply(TvShowEpisode episode) => ApplyTv(episode);

    /// <summary>
    /// Fills blank identity fields only. Curator-set or previously stored ids are left alone.
    /// </summary>
    public bool FillMissing(Film film)
    {
        var changed = false;
        if (ImdbId is not null && string.IsNullOrWhiteSpace(film.ImdbId))
        {
            film.ImdbId = ImdbId;
            film.Imdb ??= Imdb;
            changed = true;
        }
        else if (Imdb is not null && film.Imdb is null && !string.IsNullOrWhiteSpace(film.ImdbId))
        {
            film.Imdb = Imdb;
            changed = true;
        }

        if (TmdbId is not null && film.TmdbId is null)
        {
            film.TmdbId = TmdbId;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Fills blank identity fields only. Curator-set or previously stored ids are left alone.
    /// </summary>
    public bool FillMissing(ITvCanonical document)
    {
        var changed = false;
        if (ImdbId is not null && string.IsNullOrWhiteSpace(document.ImdbId))
        {
            document.ImdbId = ImdbId;
            document.Imdb ??= Imdb;
            changed = true;
        }
        else if (Imdb is not null && document.Imdb is null && !string.IsNullOrWhiteSpace(document.ImdbId))
        {
            document.Imdb = Imdb;
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
            document.Tvdb ??= Tvdb;
            changed = true;
        }
        else if (Tvdb is not null && document.Tvdb is null && document.TvdbId is not null)
        {
            document.Tvdb = Tvdb;
            changed = true;
        }

        return changed;
    }

    private void ApplyTv(ITvCanonical document)
    {
        if (ImdbId is not null)
        {
            document.ImdbId = ImdbId;
            document.Imdb = Imdb;
        }

        if (TmdbId is not null)
        {
            document.TmdbId = TmdbId;
        }

        if (TvdbId is not null)
        {
            document.TvdbId = TvdbId;
            document.Tvdb = Tvdb;
        }
    }
}
