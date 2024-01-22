using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectMatcher(ISubjectService subjectService, ILogger<SubjectMatcher> logger)
    : ISubjectMatcher
{
    public async Task<IList<SubjectMatch>> MatchSubjects(Episode episode, SubjectEnrichmentOptions? options = null)
    {
        var subjectMatches = await subjectService.Match(episode, options?.IgnoredTerms);
        var subjectMatch = subjectMatches.OrderByDescending(x => x.MatchResults.Sum(y => y.Matches));
        return subjectMatch.ToList();
    }
}