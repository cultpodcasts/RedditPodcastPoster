using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class SubjectMatchExtensions
{
    /// <summary>
    /// Projects the title/description evidence of a subject match into persisted
    /// <see cref="PlayableSubjectMatch"/> provenance (subject, matched term, source field).
    /// Match results without a source carry no evidence and are skipped; repeated
    /// (term, source) pairs (case-insensitive term) are collapsed, keeping the first.
    /// </summary>
    public static IEnumerable<PlayableSubjectMatch> ToPlayableSubjectMatches(this SubjectMatch match) =>
        match.MatchResults
            .Where(r => r.Source.HasValue)
            .Select(r => new PlayableSubjectMatch
            {
                Subject = match.Subject.Name,
                Term = r.Term,
                Source = r.Source!.Value
            })
            .DistinctBy(m => (m.Subject, m.Term.ToLowerInvariant(), m.Source));
}
