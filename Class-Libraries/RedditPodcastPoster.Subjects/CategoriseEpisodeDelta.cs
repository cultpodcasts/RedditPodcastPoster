namespace RedditPodcastPoster.Subjects;

/// <summary>
/// Subject delta for one episode within a podcast-level categorise log line.
/// </summary>
public sealed record CategoriseEpisodeDelta(
    Guid EpisodeId,
    string Title,
    IReadOnlyList<string> Before,
    IReadOnlyList<string> After,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    bool Persisted)
{
    public static CategoriseEpisodeDelta From(Guid episodeId, string title, IReadOnlyList<string> before,
        IReadOnlyList<string> after, bool persisted)
    {
        var beforeSet = before.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var afterSet = after.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = after.Where(s => !beforeSet.Contains(s)).ToArray();
        var removed = before.Where(s => !afterSet.Contains(s)).ToArray();
        return new CategoriseEpisodeDelta(episodeId, title, before, after, added, removed, persisted);
    }
}
