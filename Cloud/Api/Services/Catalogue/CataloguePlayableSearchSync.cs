using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Catalogue;

/// <summary>
/// The search and short-link step of a news report, TV episode or film edit, after the save. It brings these edits to
/// parity with the podcast episode edit (<see cref="EpisodeUpdateService"/>):
/// <list type="bullet">
/// <item><c>removed=true</c> deletes the search document and the short-link record (every time, so a retry finishes
/// the job). A migrated item keeps the short-link record it was given as a podcast episode, under the same id.</item>
/// <item>Otherwise the search document is pushed straight away (merge-or-upload under the item id), or deleted when the
/// parent is removed, the same filter as the sibling pull indexer. That indexer still re-reads the item by <c>_ts</c>,
/// so this only makes the change visible sooner.</item>
/// </list>
/// The Cosmos save has already succeeded, so a search or short-link failure is logged and does not fail the edit.
/// </summary>
public class CataloguePlayableSearchSync(
    IPlayableSearchDocumentSwap searchUpload,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IShortnerService shortnerService,
    ILogger<CataloguePlayableSearchSync> logger)
{
    public Task<EntitySearchIndexerResponse?> AfterSave(
        NewsReport report,
        PodcastEpisode shortLinkTarget,
        bool removedByChange,
        CancellationToken cancellationToken) =>
        AfterSave(
            report.Id,
            report.NewsOrganisationName ?? string.Empty,
            removedByChange,
            CataloguePlayableSearchDocument.ShouldExcludeFromSearch(report),
            () => CataloguePlayableSearchDocument.For(report),
            shortLinkTarget,
            cancellationToken);

    public Task<EntitySearchIndexerResponse?> AfterSave(
        TvShowEpisode episode,
        PodcastEpisode shortLinkTarget,
        bool removedByChange,
        CancellationToken cancellationToken) =>
        AfterSave(
            episode.Id,
            episode.TvShowName ?? string.Empty,
            removedByChange,
            CataloguePlayableSearchDocument.ShouldExcludeFromSearch(episode),
            () => CataloguePlayableSearchDocument.For(episode),
            shortLinkTarget,
            cancellationToken);

    /// <param name="trailersCleared">
    /// True when the edit removed the film's last trailer. A film with no trailers omits <c>trailers</c> (so films
    /// keep pushing before the index has the field), and merge-or-upload ignores an omitted field, so this sends
    /// <c>[]</c> to clear the stale list.
    /// </param>
    public Task<EntitySearchIndexerResponse?> AfterSave(
        Film film,
        PodcastEpisode shortLinkTarget,
        bool removedByChange,
        CancellationToken cancellationToken,
        bool trailersCleared = false) =>
        AfterSave(
            film.Id,
            film.Name,
            removedByChange,
            CataloguePlayableSearchDocument.ShouldExcludeFromSearch(film),
            () =>
            {
                var document = CataloguePlayableSearchDocument.For(film);
                if (trailersCleared && document.Trailers is null)
                {
                    document.Trailers = [];
                }

                return document;
            },
            shortLinkTarget,
            cancellationToken);

    /// <returns>The indexer state when a document was pushed; null when the step deleted (or failed before pushing).</returns>
    private async Task<EntitySearchIndexerResponse?> AfterSave(
        Guid id,
        string parentName,
        bool removedByChange,
        bool excluded,
        Func<EpisodeSearchRecord> document,
        PodcastEpisode shortLinkTarget,
        CancellationToken cancellationToken)
    {
        try
        {
            if (removedByChange)
            {
                // EpisodeSearchIndexCleanup logs and swallows its own failures.
                await searchIndexCleanup.DeleteSearchEntry(parentName, id, cancellationToken);
                await DeleteShortLink(id, shortLinkTarget);
                return null;
            }

            if (excluded)
            {
                await searchIndexCleanup.DeleteSearchEntry(parentName, id, cancellationToken);
                return null;
            }

            var pushed = await searchUpload.UploadAsync([document()], cancellationToken);
            if (!pushed)
            {
                logger.LogError("Failed to push search document '{id}' of '{parentName}' after a catalogue edit.", id,
                    parentName);
            }

            return new EntitySearchIndexerResponse
                { IndexerState = pushed ? IndexerState.Executed : IndexerState.Failure };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Cosmos save succeeded for '{id}' of '{parentName}' but the search or short-link step failed. Returning Accepted so a curator retry is not required.",
                id, parentName);
            return null;
        }
    }

    private async Task DeleteShortLink(Guid id, PodcastEpisode shortLinkTarget)
    {
        // The short-link key is the item id (base 64), the same for the pre-PATCH projection and the saved item.
        var result = await shortnerService.Delete(shortLinkTarget);
        if (!result.Success)
        {
            logger.LogWarning("Short-link record for '{id}' was not deleted (absent, or the KV delete failed).", id);
        }
    }
}
