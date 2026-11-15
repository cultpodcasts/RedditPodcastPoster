using Azure.Search.Documents.Models;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
/// Chooses the Azure AI Search write action for a playable document. Films use Upload (replace)
/// so a null <c>seriesName</c> or <c>podcastName</c> actually clears the YouTube uploader channel
/// left over from before the item became a film. MergeOrUpload plus
/// <see cref="System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull"/> omits those
/// fields, so the old values stay (RPP #1428). Other kinds still merge, so an omitted field is not wiped.
/// </summary>
public static class SearchIndexDocumentActions
{
    public static IndexDocumentsAction<EpisodeSearchRecord> For(EpisodeSearchRecord document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.ContentKind == SearchContentKind.Film)
        {
            return IndexDocumentsAction.Upload(document);
        }

        return IndexDocumentsAction.MergeOrUpload(document);
    }

    public static IndexDocumentsBatch<EpisodeSearchRecord> BatchFor(IEnumerable<EpisodeSearchRecord> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var batch = new IndexDocumentsBatch<EpisodeSearchRecord>();
        foreach (var document in documents)
        {
            batch.Actions.Add(For(document));
        }

        return batch;
    }
}
