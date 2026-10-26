using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

/// <summary>
/// The search document for one stored TV episode, news report or film, shaped like the sibling Cosmos pull
/// (<c>PlayableSearchSources</c>): <c>title</c> is the playable title (a film's name), <c>seriesName</c> is the
/// denormalised parent name (none for a film), <c>publisherSearchTerms</c> is the denormalised parent terms
/// (none for a film) and <c>episodeSearchTerms</c> is the item's own terms. Used by the curator edit to push the
/// document straight away instead of waiting for the pull indexer's next <c>_ts</c> run.
/// </summary>
public static class CataloguePlayableSearchDocument
{
    public static EpisodeSearchRecord For(TvShowEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        EpisodeServicePresence.SyncCatalogueYouTubeId(episode);
        var show = new TvShow(episode.TvShowName ?? string.Empty) { Id = episode.TvShowId };
        var record = CatalogueParentKindMapper.ToPostProjection(episode, show)
            .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
        record.ContentKind = SearchContentKind.TvShowEpisode;
        record.SeriesName = episode.TvShowName?.Trim() ?? string.Empty;
        record.PublisherSearchTerms = episode.PublisherSearchTerms ?? string.Empty;
        return record;
    }

    public static EpisodeSearchRecord For(NewsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        EpisodeServicePresence.SyncCatalogueYouTubeId(report);
        var organisation = new NewsOrganisation(report.NewsOrganisationName ?? string.Empty)
        {
            Id = report.NewsOrganisationId
        };
        var record = CatalogueParentKindMapper.ToPostProjection(report, organisation)
            .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
        record.ContentKind = SearchContentKind.NewsReport;
        record.SeriesName = report.NewsOrganisationName?.Trim() ?? string.Empty;
        record.PublisherSearchTerms = report.PublisherSearchTerms ?? string.Empty;
        return record;
    }

    public static EpisodeSearchRecord For(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        EpisodeServicePresence.SyncCatalogueYouTubeId(film);
        var record = CatalogueParentKindMapper.ToPostProjection(film)
            .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
        record.ContentKind = SearchContentKind.Film;
        record.SeriesName = null;
        record.PublisherSearchTerms = string.Empty;
        record.Trailers = FilmTrailerSearchToken.For(film);
        return record;
    }

    /// <summary>The pull query's filter: a removed item, or one whose parent is removed, has no document.</summary>
    public static bool ShouldExcludeFromSearch(Playable playable) =>
        playable.IsRemoved() || playable.ParentRemoved == true;

    public static bool ShouldExcludeFromSearch(Film film) => film.IsRemoved();
}
