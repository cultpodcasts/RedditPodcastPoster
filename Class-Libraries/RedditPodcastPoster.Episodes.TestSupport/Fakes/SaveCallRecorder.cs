namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

public enum EpisodeSaveKind
{
    Single,
    Batch
}

public sealed record EpisodeSaveCall(
    EpisodeSaveKind Kind,
    IReadOnlyList<Guid> EpisodeIds,
    DateTime UtcTimestamp);

public sealed class SaveCallRecorder
{
    private readonly List<EpisodeSaveCall> _episodeCalls = [];

    public IReadOnlyList<EpisodeSaveCall> EpisodeCalls => _episodeCalls;

    internal void RecordSingle(Guid episodeId) =>
        _episodeCalls.Add(new EpisodeSaveCall(EpisodeSaveKind.Single, [episodeId], DateTime.UtcNow));

    internal void RecordBatch(IEnumerable<Guid> episodeIds) =>
        _episodeCalls.Add(new EpisodeSaveCall(
            EpisodeSaveKind.Batch,
            episodeIds.ToList(),
            DateTime.UtcNow));
}
