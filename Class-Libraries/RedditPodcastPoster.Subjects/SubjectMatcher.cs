using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectMatcher : ISubjectMatcher
{
    private readonly ILogger<SubjectMatcher> _logger;
    private readonly ISubjectService _subjectService;

    public SubjectMatcher(ISubjectService subjectService, ILogger<SubjectMatcher> logger)
    {
        _subjectService = subjectService;
        _logger = logger;
    }

    public async Task<IList<SubjectMatch>> MatchSubjects(Episode episode, SubjectEnrichmentOptions? options = null)
    {
        var subjectMatches = await _subjectService.Match(episode, options?.IgnoredTerms);
        var subjectMatch = subjectMatches.OrderByDescending(x => x.MatchResults.Sum(y => y.Matches));
        return subjectMatch.ToList();
    }
}