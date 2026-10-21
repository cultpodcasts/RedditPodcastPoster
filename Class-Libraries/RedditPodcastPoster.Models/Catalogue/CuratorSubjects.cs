namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// The curator "replace subjects" rule, shared by <see cref="Playable"/> and <c>Film</c> (a publisher that is also playable).
/// Subjects the curator drops are remembered in removed subjects; subjects they add back are taken out of it;
/// subject matches for dropped subjects are discarded.
/// </summary>
public static class CuratorSubjects
{
    /// <returns>Whether anything changed, and the subject list to store (the current list when unchanged).</returns>
    public static (bool Changed, List<string> Subjects) Apply(
        List<string> subjects,
        List<string> removedSubjects,
        List<PlayableSubjectMatch> matches,
        IEnumerable<string> newSubjects)
    {
        var newList = newSubjects.ToList();
        if (subjects.SequenceEqual(newList))
        {
            return (false, subjects);
        }

        var newSet = new HashSet<string>(newList, StringComparer.OrdinalIgnoreCase);
        foreach (var subject in subjects)
        {
            if (!newSet.Contains(subject) &&
                !removedSubjects.Contains(subject, StringComparer.OrdinalIgnoreCase))
            {
                removedSubjects.Add(subject);
            }
        }

        removedSubjects.RemoveAll(s => newSet.Contains(s));
        matches.RemoveAll(m => !newSet.Contains(m.Subject, StringComparer.OrdinalIgnoreCase));
        return (true, newList);
    }
}
