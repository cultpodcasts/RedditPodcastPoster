namespace RedditPodcastPoster.TheTvdb.Models;

public enum TheTvdbSearchType
{
    Series,
    Movie
}

/// <summary>One TheTVDB search hit, with the public canonical page when the slug is present.</summary>
public sealed record TheTvdbSearchHit(
    long Id,
    string Name,
    TheTvdbSearchType Type,
    string? Year,
    string? Country,
    string? Network,
    string? Slug,
    string? ImdbTitleId,
    Uri? CanonicalUrl,
    Uri? ImdbUrl);

/// <summary>
/// A TheTVDB series record.
/// <see cref="TmdbSeriesId"/> is the TMDB series id from the extended record
/// (TheMovieDB.com title id, source type 12). A collection id that shares that source name is not stored.
/// </summary>
public sealed record TheTvdbSeries(
    long Id,
    string Name,
    string? Year,
    string? Country,
    string? Slug,
    string? ImdbTitleId,
    int? TmdbSeriesId,
    Uri? CanonicalUrl,
    Uri? ImdbUrl);

/// <summary>
/// A TheTVDB episode record. <see cref="CanonicalUrl"/> is the episode page, not the series page.
/// <see cref="TmdbSeriesId"/> is the parent series id from the series record, so it can be passed to a series lookup.
/// <see cref="TmdbEpisodeId"/> is the TheMovieDB.com id on this episode row. It is an episode id, not a series id.
/// </summary>
public sealed record TheTvdbEpisode(
    long Id,
    long SeriesId,
    string Name,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? Year,
    string? ImdbTitleId,
    int? TmdbSeriesId,
    int? TmdbEpisodeId,
    Uri? CanonicalUrl,
    Uri? ImdbUrl);
