using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Services.SubmitUrl;

/// <summary>
/// Indexes a news report / TV episode a submit just saved (toolbar submit and discovery), with the same document as
/// kind transfer (search key = playable id). A failure is logged, never thrown: the item is already stored.
/// </summary>
public static class CatalogueSubmitSearchIndexing
{
    /// <returns>True when the document was uploaded.</returns>
    public static async Task<bool> IndexAsync(
        IPlayableSearchDocumentSwap playableSearch,
        CatalogueSearchSource source,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var contentKind = source.Kind == CatalogueParentKind.NewsOrganisation
            ? SearchContentKind.NewsReport
            : SearchContentKind.TvShowEpisode;
        try
        {
            var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
                source.Parent, [source.Episode], contentKind);
            if (await playableSearch.UploadAsync(documents, cancellationToken))
            {
                return true;
            }

            logger.LogError("Failed to index {ContentKind} '{PlayableId}' after submission.", contentKind,
                source.Episode.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index {ContentKind} '{PlayableId}' after submission.", contentKind,
                source.Episode.Id);
        }

        return false;
    }
}
