using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

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
        var subjectMatches = await _subjectService.Match(episode, ignoredTerms);
        var subjectMatch = subjectMatches.GroupBy(x => x.MatchResults.Sum(y => y.Matches)).MaxBy(x => x.Key);
        if (subjectMatch != null)
        {
            _logger.LogInformation(
                $"{subjectMatch.Count()} - {string.Join(",", subjectMatch.Select(x => "'" + x.Subject.Name + "' (" + x.MatchResults.MaxBy(x => x.Matches)?.Term + ")"))} : '{episode.Title}'.");
            episode.Subject = string.Join(", ", subjectMatch.Select(x => x.Subject.Name));
        }
        else
        {
            _logger.LogInformation($"'No match: '{episode.Title}'.");
        }
    }
}