using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Catalogue.Identity;

/// <summary>
/// Reads a film's or TV episode's own metadata from its identity services (RPP #1430). Lookups never throw: a failed
/// or missing lookup is null, so a save never fails because TMDB or TheTVDB is down.
/// Apply the result with <see cref="IdentityMetadataFill"/>, which fills only empty fields.
/// </summary>
public interface IIdentityMetadataEnricher
{
    /// <summary>The TMDB movie for the film's <c>tmdbId</c>, or for its <c>imdbId</c> when it has no TMDB id.</summary>
    Task<TmdbMovieMetadata?> FetchFilmAsync(
        int? tmdbId,
        string? imdbId,
        CancellationToken cancellationToken = default);

    /// <summary>The TMDB series for the show's <c>tmdbId</c>, or for its <c>imdbId</c> when it has no TMDB id.</summary>
    Task<TmdbShowMetadata?> FetchTvShowAsync(
        int? tmdbId,
        string? imdbId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The TMDB episode for a TV episode. Its series, season and episode number come from its TheTVDB episode
    /// (with <paramref name="showTmdbId"/>, the parent show's TMDB series id, preferred), else from TMDB's find by its
    /// IMDb id.
    /// </summary>
    Task<TmdbEpisodeMetadata?> FetchTvShowEpisodeAsync(
        TvShowEpisode episode,
        int? showTmdbId,
        CancellationToken cancellationToken = default);
}
