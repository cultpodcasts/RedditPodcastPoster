using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// Index-flow miss memory recorded during a DI scope (typically one Indexer activity). Platforms are never
/// cross-used. <see cref="PodcastUpdater"/> applies <see cref="TryGetChanges"/> onto
/// <see cref="Podcast.LookupMisses"/> before the existing podcast save.
/// </summary>
public interface ICatalogueLookupMisses
{
    void RecordMiss(
        Guid podcastId,
        Guid episodeId,
        string platform,
        string? newestCatalogueId,
        int? showTotal,
        DateTime lastFullAttemptUtc);

    void Clear(Guid podcastId, Guid episodeId, string platform);

    /// <summary>
    /// Pending record/clear mutations for <paramref name="podcastId"/> this scope. False when none.
    /// </summary>
    bool TryGetChanges(Guid podcastId, out IReadOnlyList<CatalogueLookupMissChange> changes);
}

/// <summary>One pending mutation for a podcast's <see cref="Podcast.LookupMisses"/> list.</summary>
public sealed record CatalogueLookupMissChange(
    Guid EpisodeId,
    string Platform,
    bool Clear,
    string? NewestCatalogueId = null,
    int? ShowTotal = null,
    DateTime? LastFullAttemptUtc = null);

/// <inheritdoc />
public sealed class CatalogueLookupMisses : ICatalogueLookupMisses
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        (Guid PodcastId, Guid EpisodeId, string Platform), CatalogueLookupMissChange> _changes = new();

    public void RecordMiss(
        Guid podcastId,
        Guid episodeId,
        string platform,
        string? newestCatalogueId,
        int? showTotal,
        DateTime lastFullAttemptUtc)
    {
        if (podcastId == Guid.Empty || episodeId == Guid.Empty || string.IsNullOrWhiteSpace(platform))
        {
            return;
        }

        _changes[(podcastId, episodeId, platform)] = new CatalogueLookupMissChange(
            episodeId, platform, Clear: false, newestCatalogueId, showTotal, lastFullAttemptUtc);
    }

    public void Clear(Guid podcastId, Guid episodeId, string platform)
    {
        if (podcastId == Guid.Empty || episodeId == Guid.Empty || string.IsNullOrWhiteSpace(platform))
        {
            return;
        }

        _changes[(podcastId, episodeId, platform)] = new CatalogueLookupMissChange(
            episodeId, platform, Clear: true);
    }

    public bool TryGetChanges(Guid podcastId, out IReadOnlyList<CatalogueLookupMissChange> changes)
    {
        var list = _changes
            .Where(x => x.Key.PodcastId == podcastId)
            .Select(x => x.Value)
            .ToList();
        changes = list;
        return list.Count > 0;
    }
}
