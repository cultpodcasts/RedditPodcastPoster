namespace CreateSearchIndex;

/// <summary>
/// What a CreateSearchIndex run does to the sibling TvShowEpisode, Film and NewsReport
/// datasources and indexers (<see cref="PlayableSearchSources"/>) that feed the shared index.
/// <para>
/// A teardown deletes the whole index, migrated documents included. The siblings' high-water marks
/// would then sit past every existing TV episode, film and news report, so a later scheduled run
/// never re-pushes them. A teardown therefore always deletes and recreates the siblings, even
/// without <c>--all-playables</c>, so the recreated index gets every playable back.
/// </para>
/// <para>
/// <see cref="TransientSiblings"/> (<c>--transient-playables</c>) creates, runs and deletes one sibling
/// at a time after the episode indexer, so a Free-tier service (three data sources and three indexers)
/// never needs more than two of each. Nothing is left scheduled for the siblings afterwards.
/// </para>
/// </summary>
public readonly record struct SearchIndexRebuildPlan(
    bool TearDownSiblings,
    bool EnsureSiblings,
    bool ResetSiblings,
    bool RunSiblings,
    bool TransientSiblings = false)
{
    public static SearchIndexRebuildPlan For(CreateSearchIndexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hasIndex = !string.IsNullOrWhiteSpace(request.IndexName);
        var tearDown = hasIndex && request.TearDownIndex;
        var ensure = hasIndex && (request.AllPlayables || tearDown);
        if (request.TransientPlayables)
        {
            if (request.UpdateExisting)
            {
                throw new InvalidOperationException(
                    "Refusing --transient-playables together with --update-existing (transient siblings are deleted after their run).");
            }

            if (!ensure || !request.RunIndexer)
            {
                throw new InvalidOperationException(
                    "--transient-playables needs --index, --run-indexer and either --teardown-index or --all-playables.");
            }

            return new SearchIndexRebuildPlan(
                TearDownSiblings: tearDown,
                EnsureSiblings: false,
                ResetSiblings: false,
                RunSiblings: false,
                TransientSiblings: true);
        }

        return new SearchIndexRebuildPlan(
            TearDownSiblings: tearDown,
            EnsureSiblings: ensure,
            // --update-existing --reset-indexer resets the episode indexer so existing documents pick up
            // new fields; with --all-playables the siblings need the same reset or theirs never do.
            ResetSiblings: hasIndex && request.UpdateExisting && request.ResetIndexer && request.AllPlayables,
            RunSiblings: ensure && request.RunIndexer);
    }
}
