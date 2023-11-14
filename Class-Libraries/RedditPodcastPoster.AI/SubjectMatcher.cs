using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;

public class SubjectMatcher : ISubjectMatcher
{
    private readonly ILogger<ISubjectMatcher> _logger;
    private readonly ISubjectService _subjectService;

    public SubjectMatcher(
        ISubjectService subjectService,
        ILogger<ISubjectMatcher> logger)
    {
        _subjectService = subjectService;
        _logger = logger;
    }

    public async Task MatchSubject(Episode episode, string[]? ignoredTerms = null)
    {
        var originalSubject = episode.Subject;
        var subjects = await _subjectService.Match(episode, ignoredTerms);
        var subject = subjects.MaxBy(x => x.MatchResults.Sum(y => y.Matches));

        episode.Subject = subject?.Subject.Name;

        var updated = episode.Subject != originalSubject;
        if (!updated)
        {
            var descriptionSubjects = await _subjectService.Match(episode, ignoredTerms);
            var matchedSubjects = descriptionSubjects.GroupBy(x => x.MatchResults.Sum(y => y.Matches))
                .OrderByDescending(x => x.Key);
            episode.Subject = string.Join(",", matchedSubjects);
        }
    }
}