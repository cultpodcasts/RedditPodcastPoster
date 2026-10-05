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

/// <summary>A TheTVDB series record.</summary>
public sealed record TheTvdbSeries(
    long Id,
    string Name,
    string? Year,
    string? Country,
    string? Slug,
    string? ImdbTitleId,
    Uri? CanonicalUrl,
    Uri? ImdbUrl);

/// <summary>A TheTVDB episode record. <see cref="CanonicalUrl"/> is the episode page, not the series page.</summary>
public sealed record TheTvdbEpisode(
    long Id,
    long SeriesId,
    string Name,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? Year,
    string? ImdbTitleId,
    Uri? CanonicalUrl,
    Uri? ImdbUrl);
