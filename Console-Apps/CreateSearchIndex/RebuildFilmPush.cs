using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

namespace CreateSearchIndex;

/// <summary>#1408: pushes every film into an index that CreateSearchIndex has just rebuilt.</summary>
public interface IRebuildFilmPush
{
    /// <returns>True when every film was pushed into <paramref name="indexName"/>.</returns>
    Task<bool> PushAfterRebuild(string indexName, CancellationToken cancellationToken);
}

/// <summary>
/// #1408: after <c>--teardown-index --run-indexer</c>, pushes every film into the recreated index in the same run, so
/// a rebuild needs no separate FilmSearchPusher step. Films have no pull indexer (the Free tier's three data sources
/// are used by episodes, TV show episodes and news reports).
/// </summary>
/// <remarks>
/// <see cref="IFilmSearchPusher"/> writes to the configured search index (<see cref="SearchClient.IndexName"/>). A
/// rebuild of any other index name is not pushed to, because the films would land in the wrong index; the log says to
/// run FilmSearchPusher against that index instead. A failed push is logged, not thrown: the index and its indexers
/// are already rebuilt, and the scheduled refresh's film push finds the empty film set and pushes every film.
/// </remarks>
public sealed class RebuildFilmPush(
    IFilmSearchPusher filmSearchPusher,
    SearchClient searchClient,
    ILogger<RebuildFilmPush> logger) : IRebuildFilmPush
{
    public async Task<bool> PushAfterRebuild(string indexName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        if (!string.Equals(indexName, searchClient.IndexName, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Rebuilt index '{IndexName}' is not the configured search index '{ConfiguredIndexName}', so films were not pushed. " +
                "Run FilmSearchPusher --full with that index configured.",
                indexName, searchClient.IndexName);
            return false;
        }

        try
        {
            var result = await filmSearchPusher.Push(new FilmSearchPushRequest(Full: true), cancellationToken);
            if (result.Succeeded)
            {
                logger.LogInformation(
                    "Pushed every film into the rebuilt index '{IndexName}': read {FilmsRead}, pushed {ToPush}, deleted {ToDelete}.",
                    indexName, result.FilmsRead, result.ToPush, result.ToDelete);
                return true;
            }

            logger.LogError(
                "Film push into the rebuilt index '{IndexName}' was incomplete: pushed {ToPush}, deleted {ToDelete}, failed {Failed}. " +
                "Run FilmSearchPusher --full, or wait for the scheduled refresh to push them.",
                indexName, result.ToPush, result.ToDelete, result.Failed);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Film push into the rebuilt index '{IndexName}' failed. Run FilmSearchPusher --full, or wait for the scheduled refresh to push them.",
                indexName);
            return false;
        }
    }
}
