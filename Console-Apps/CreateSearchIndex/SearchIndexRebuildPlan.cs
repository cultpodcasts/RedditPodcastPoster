namespace CreateSearchIndex;

/// <summary>
/// What a CreateSearchIndex run does to the sibling TvShowEpisode and NewsReport datasources and indexers
/// (<see cref="PlayableSearchSources"/>) that feed the shared index.
/// <para>
/// The standard layout is three data sources and three indexers (the Free-tier limit): episodes, TV show episodes
/// and news reports. Films have no pull indexer: they are pushed by Cosmos <c>_ts</c> (the Indexer's scheduled refresh
/// and the FilmSearchPusher console, #1408).
/// </para>
/// <para>
/// A teardown deletes the whole index, migrated documents included. The siblings' high-water marks would then sit
/// past every existing TV episode and news report, so a later scheduled run never re-pushes them. A teardown
/// therefore always deletes and recreates the siblings, even without <c>--all-playables</c>, so the recreated index
/// gets every TV episode and news report back. With <c>--run-indexer</c> every film is pushed in the same run
/// (<see cref="PushFilms"/>); without it the scheduled refresh pushes them back within the hour.
/// </para>
/// <para>
/// <c>--all-playables</c> without a teardown adds (or updates) the TV and news siblings on the existing index, so a
/// service that has only the episode pair can be brought to the standard layout without a rebuild.
/// </para>
/// </summary>
public readonly record struct SearchIndexRebuildPlan(
    bool TearDownSiblings,
    bool EnsureSiblings,
    bool ResetSiblings,
    bool RunSiblings,
    bool PushFilms)
{
    public static SearchIndexRebuildPlan For(CreateSearchIndexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hasIndex = !string.IsNullOrWhiteSpace(request.IndexName);
        var tearDown = hasIndex && request.TearDownIndex;
        var ensure = hasIndex && (request.AllPlayables || tearDown);
        return new SearchIndexRebuildPlan(
            TearDownSiblings: tearDown,
            EnsureSiblings: ensure,
            // --update-existing --reset-indexer resets the episode indexer so existing documents pick up
            // new fields; with --all-playables the siblings need the same reset or theirs never do.
            ResetSiblings: hasIndex && request.UpdateExisting && request.ResetIndexer && request.AllPlayables,
            RunSiblings: ensure && request.RunIndexer,
            // #1408: a teardown deleted every film document; push them all back with the indexer run.
            PushFilms: tearDown && request.RunIndexer);
    }
}
