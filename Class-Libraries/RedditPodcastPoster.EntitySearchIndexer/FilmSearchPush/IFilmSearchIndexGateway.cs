using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

/// <summary>
/// The search-index calls the film push needs, kept narrow so the push rules can be tested without the Azure SDK.
/// Every call is limited to <c>contentKind eq 'Film'</c> documents or to explicit film ids.
/// </summary>
public interface IFilmSearchIndexGateway
{
    /// <summary>Number of documents in the index whose <c>contentKind</c> is <c>Film</c>.</summary>
    Task<long> CountFilmDocuments(CancellationToken cancellationToken);

    /// <summary>Ids of every document in the index whose <c>contentKind</c> is <c>Film</c>.</summary>
    Task<IReadOnlyList<string>> GetFilmDocumentIds(CancellationToken cancellationToken);

    /// <summary>Merge-or-upload, the same write the pull indexer and the curator edit use.</summary>
    /// <returns>Keys that failed (empty when every document succeeded).</returns>
    Task<IReadOnlyList<string>> MergeOrUpload(IReadOnlyList<EpisodeSearchRecord> documents, CancellationToken cancellationToken);

    /// <returns>Keys that failed (empty when every delete succeeded; deleting an absent key succeeds).</returns>
    Task<IReadOnlyList<string>> Delete(IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
