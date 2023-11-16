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

    public async Task MatchSubject(Episode episode, string[]? ignoredTerms = null, string? defaultSubject = null)
    {
        var subjectMatches = await _subjectService.Match(episode, ignoredTerms);
        var subjectMatch = subjectMatches.OrderByDescending(x => x.MatchResults.Sum(y => y.Matches));
        if (subjectMatch != null && subjectMatch.Any())
        {
            var message =
                $"{subjectMatch.Count()} - {string.Join(",", subjectMatch.Select(x => "'" + x.Subject.Name + "' (" + x.MatchResults.MaxBy(x => x.Matches)?.Term + ")"))} : '{episode.Title}'.";
            if (subjectMatch.Count() > 1)
            {
                _logger.LogWarning(message);
            }
            else
            {
                _logger.LogInformation(message);
            }

            episode.Subjects = subjectMatch.Select(x => x.Subject.Name).ToList();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(defaultSubject))
            {
                episode.Subjects = new[] {defaultSubject}.ToList();
                _logger.LogWarning(
                    $"Applying default-subject '{defaultSubject}' to episode with title: '{episode.Title}'.");
            }
            else
            {
                _logger.LogError($"'No match: '{episode.Title}'.");
            }
        }
    }
}