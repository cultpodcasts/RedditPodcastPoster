using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

/// <summary>
/// The search document for one stored TV episode, news report or film, shaped like the sibling Cosmos pull
/// (<c>PlayableSearchSources</c>): <c>title</c> is the playable title (a film's name), <c>seriesName</c> is the
/// denormalised parent name (none for a film), <c>publisherSearchTerms</c> is the denormalised parent terms
/// (none for a film) and <c>episodeSearchTerms</c> is the item's own terms. Used by the curator edit to push the
/// document straight away instead of waiting for the pull indexer's next <c>_ts</c> run. <c>spotifyId</c>,
/// <c>appleId</c> and <c>podcastAppleId</c> come from the <c>services</c> URLs through
/// <see cref="PlayableServiceSearchIds"/>, the same rules the pull SQL uses. TV and news are
/// merge-or-uploaded, so a null here is omitted and the ids the pull wrote stay. Films are
/// uploaded (replaced), so a null field is cleared (RPP #1428).
/// </summary>
public static class CataloguePlayableSearchDocument
{
    public static EpisodeSearchRecord For(TvShowEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        // Do not SyncCatalogueYouTubeId here — search-doc build must not mutate domain state.
        // youtubeId on the record comes from services.youtube.url via YouTubeEpisodeId on the projection.
        var show = new TvShow(episode.TvShowName ?? string.Empty) { Id = episode.TvShowId };
        var record = CatalogueParentKindMapper.ToPostProjection(episode, show)
            .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
        record.ContentKind = SearchContentKind.TvShowEpisode;
        record.SeriesName = episode.TvShowName?.Trim() ?? string.Empty;
        record.PublisherSearchTerms = episode.PublisherSearchTerms ?? string.Empty;
        PreferCatalogueYouTubeId(record, episode.YouTubeId);
        record.YouTubeAlternates = YouTubeAlternateSearchToken.For(episode);
        ApplyServiceIds(record, episode.Services);
        UseIdentityImageWhenNoServiceImage(record, episode.Image);
        record.ImdbId = string.IsNullOrWhiteSpace(episode.ImdbId) ? null : episode.ImdbId;
        record.TmdbId = episode.TmdbId;
        record.TvdbId = episode.TvdbId;
        record.SeriesImdbId = string.IsNullOrWhiteSpace(episode.SeriesImdbId) ? null : episode.SeriesImdbId;
        record.SeriesTmdbId = episode.SeriesTmdbId;
        record.SeriesTvdbId = episode.SeriesTvdbId;
        return record;
    }

    public static EpisodeSearchRecord For(NewsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var organisation = new NewsOrganisation(report.NewsOrganisationName ?? string.Empty)
        {
            Id = report.NewsOrganisationId
        };
        var record = CatalogueParentKindMapper.ToPostProjection(report, organisation)
            .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
        record.ContentKind = SearchContentKind.NewsReport;
        record.SeriesName = report.NewsOrganisationName?.Trim() ?? string.Empty;
        record.PublisherSearchTerms = report.PublisherSearchTerms ?? string.Empty;
        PreferCatalogueYouTubeId(record, report.YouTubeId);
        record.YouTubeAlternates = YouTubeAlternateSearchToken.For(report);
        ApplyServiceIds(record, report.Services);
        return record;
    }

    public static EpisodeSearchRecord For(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        var record = CatalogueParentKindMapper.ToPostProjection(film)
            .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
        record.ContentKind = SearchContentKind.Film;
        record.SeriesName = null;
        record.PodcastName = null;
        record.PublisherSearchTerms = string.Empty;
        record.Trailers = FilmTrailerSearchToken.For(film);
        // [] clears titles already in the index. Null (never set) is omitted from the JSON; on a
        // film Upload (replace) that also clears the index field, unlike a merge (RPP #1428).
        record.AlternativeTitles = film.AlternativeTitles is { } titles ? [.. titles] : null;
        record.ImdbId = string.IsNullOrWhiteSpace(film.ImdbId) ? null : film.ImdbId;
        record.TmdbId = film.TmdbId;
        PreferCatalogueYouTubeId(record, film.YouTubeId);
        record.YouTubeAlternates = YouTubeAlternateSearchToken.For(film);
        ApplyServiceIds(record, film.Services);
        // Service image wins; then identity (TMDB); then the first trailer thumbnail (RPP #1430 / #1422).
        UseIdentityImageWhenNoServiceImage(record, FilmDisplayImage.PreferIdentityOrTrailer(film));
        return record;
    }

    /// <summary>
    /// RPP #1430 / #1422: a streaming service's image wins; the identity image (TMDB backdrop, poster or still) or
    /// a trailer thumbnail is used only when no service has one. Same order as the pull SQL for services + identity
    /// (<c>PlayableSearchSql.SiblingQuery</c>); trailer fallback is push-path for films with no Services row.
    /// </summary>
    private static void UseIdentityImageWhenNoServiceImage(EpisodeSearchRecord record, Uri? identityImage)
    {
        if (string.IsNullOrEmpty(record.Image) && identityImage is not null)
        {
            record.Image = identityImage.ToString();
        }
    }

    /// <summary>
    /// When the catalogue entity already has a compact id, put it on the search record without
    /// mutating the entity. Otherwise leave the projection-derived value (services URL extract).
    /// </summary>
    private static void PreferCatalogueYouTubeId(EpisodeSearchRecord record, string? catalogueYouTubeId)
    {
        if (!string.IsNullOrWhiteSpace(catalogueYouTubeId))
        {
            record.YoutubeId = catalogueYouTubeId;
        }
    }

    private static void ApplyServiceIds(
        EpisodeSearchRecord record,
        IReadOnlyDictionary<string, ServiceLink>? services)
    {
        (record.SpotifyId, record.AppleId, record.PodcastAppleId) = PlayableServiceSearchIds.From(services);
    }

    /// <summary>
    /// Denormalised-only exclusion. Prefer
    /// <see cref="CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(TvShow, TvShowEpisode)"/> /
    /// <see cref="CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(NewsOrganisation, NewsReport)"/>
    /// when the live parent is available (Api push / Index), so a lagging <c>ParentRemoved</c> cannot re-upload.
    /// </summary>
    public static bool ShouldExcludeFromSearch(Playable playable) =>
        CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(playable);

    public static bool ShouldExcludeFromSearch(Film film) =>
        CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(film);
}
