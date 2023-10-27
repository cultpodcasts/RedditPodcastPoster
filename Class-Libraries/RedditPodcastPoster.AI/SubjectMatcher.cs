using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;

public class SubjectMatcher : ISubjectMatcher
{
    private readonly ISubjectService _subjectService;
    private readonly ILogger<ISubjectMatcher> _logger;

    public SubjectMatcher(
        ISubjectService subjectService,
        ILogger<ISubjectMatcher> logger)
    {
        _subjectService = subjectService;
        _logger = logger;
    }

    public async Task MatchSubject(Episode episode, string? originalSubject)
    {
        var subjects = await _subjectService.Match(episode, false);
        var subject =
            subjects
                .GroupBy(y => y)
                .OrderByDescending(g => g.Count())
                .SelectMany(g => g).ToList()
                .FirstOrDefault();
        episode.Subject = subject;

        var updated = episode.Subject != originalSubject;
        if (!updated)
        {
            var descriptionSubjects = await _subjectService.Match(episode, true);
            var descriptionSubject =
                descriptionSubjects
                    .GroupBy(y => y)
                    .OrderByDescending(g => g.Count())
                    .SelectMany(g => g).ToList()
                    .FirstOrDefault();
            episode.Subject = descriptionSubject;
        }
    }
}