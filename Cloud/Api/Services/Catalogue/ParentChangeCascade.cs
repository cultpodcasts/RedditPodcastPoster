using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.Catalogue;

/// <summary>
/// Re-projects a news organisation's or TV show's children after a curator edit. This mirrors the podcast precedent in
/// <c>PodcastUpdateService</c> / <c>PodcastEpisodeProjectionHelper</c>:
/// <list type="bullet">
/// <item>The parent's search terms, language and removed are copied onto each child (<c>publisherSearchTerms</c>,
/// <c>publisherLanguage</c>, <c>parentRemoved</c>). A child's own <c>removed</c> is never touched, so a child removed
/// individually before stays removed when its parent is restored.</item>
/// <item>When the parent is removed, every child's search document is deleted (the sibling pull indexers never delete).
/// Deleting an id that is not in the index succeeds.</item>
/// <item>When the parent is restored, <c>parentRemoved</c> is cleared and the child is saved, which bumps <c>_ts</c>. The
/// sibling indexer, whose SQL excludes <c>parentRemoved</c> and <c>removed</c> children, re-adds it on its next run.</item>
/// </list>
/// Children are read once. Only children that actually changed are saved, in batches. The search delete always runs on
/// removal, so retrying the same PATCH after a partial failure finishes the job without rewriting unchanged children.
/// </summary>
public static class ParentChangeCascade
{
    internal const int SaveBatchSize = 100;

    public sealed record Result(int Children, int Saved, bool SearchDeleteSucceeded, IReadOnlyList<Guid> ChildIds);

    public static async Task<Result> Run<TChild>(
        IAsyncEnumerable<TChild> children,
        Func<TChild, bool> reproject,
        Func<IEnumerable<TChild>, Task> save,
        bool parentRemovedRequested,
        string parentName,
        EpisodeSearchIndexCleanup searchIndexCleanup,
        ILogger logger,
        CancellationToken cancellationToken) where TChild : Playable
    {
        var all = await children.ToListAsync(cancellationToken);
        var changed = all.Where(reproject).ToArray();
        foreach (var batch in changed.Chunk(SaveBatchSize))
        {
            await save(batch);
        }

        if (changed.Length > 0)
        {
            logger.LogInformation("{method}: Re-projected {count} of {total} child item(s) of '{parentName}'.",
                nameof(Run), changed.Length, all.Count, parentName);
        }

        var searchDeleteSucceeded = true;
        if (parentRemovedRequested && all.Count > 0)
        {
            searchDeleteSucceeded = await searchIndexCleanup.DeleteSearchEntries(
                parentName, all.Select(child => child.Id).ToArray(), cancellationToken);
        }

        return new Result(all.Count, changed.Length, searchDeleteSucceeded, all.Select(child => child.Id).ToArray());
    }
}
