using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectMatcher(
    ISubjectService subjectService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SubjectMatcher> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISubjectMatcher
{
    public async Task<IList<SubjectMatch>> MatchSubjects(
        Episode episode,
        SubjectEnrichmentOptions? options = null)
    {
        var subjectMatches = await subjectService.Match(
            episode,
            options?.IgnoredAssociatedSubjects,
            options?.IgnoredSubjects);
        var subjectMatch = subjectMatches.OrderByDescending(x => x.MatchResults.Sum(y => y.Matches));
        return subjectMatch.ToList();
    }
}