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
