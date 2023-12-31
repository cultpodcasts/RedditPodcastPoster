﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectEnricher : ISubjectEnricher
{
    private readonly ILogger<ISubjectEnricher> _logger;
    private readonly ISubjectMatcher _subjectMatcher;

    public SubjectEnricher(
        ISubjectMatcher subjectMatcher,
        ILogger<ISubjectEnricher> logger)
    {
        _subjectMatcher = subjectMatcher;
        _logger = logger;
    }

    public async Task EnrichSubjects(Episode episode, SubjectEnrichmentOptions? options = null)
    {
        var subjectMatches = await _subjectMatcher.MatchSubjects(episode, options);
        var (additions, removals) = CompareSubjects(episode.Subjects, subjectMatches, options?.DefaultSubject);

        if (additions.Any())
        {
            var message =
                $"{additions.Count()} - {string.Join(",", additions.Select(x => "'" + x.Subject.Name + "' (" + x.MatchResults.MaxBy(x => x.Matches)?.Term + ")"))} : '{episode.Title}'.";
            if (!episode.Subjects.Any() && additions.Count() > 1)
            {
                _logger.LogWarning(message);
            }
            else
            {
                _logger.LogInformation(message);
            }

            episode.Subjects.AddRange(additions.Select(x => x.Subject.Name));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(options?.DefaultSubject) && !episode.Subjects
                    .Select(x => x.ToLowerInvariant()).Contains(options.DefaultSubject.ToLowerInvariant()))
            {
                episode.Subjects = new[] {options.DefaultSubject}.ToList();
                _logger.LogWarning(
                    $"Applying default-subject '{options.DefaultSubject}' to episode with title: '{episode.Title}'.");
            }
            else if (!episode.Subjects.Any())
            {
                _logger.LogError($"'No updates: '{episode.Title}'.");
            }
        }

        if (removals.Any())
        {
            _logger.LogWarning(
                $"Redundant: {string.Join(",", removals.Select(x => "'" + x + "'"))} : '{episode.Title}'.");
        }
    }

    private (IList<SubjectMatch>, IList<string>) CompareSubjects(
        IList<string> existingSubjects,
        IList<SubjectMatch> matches,
        string? defaultSubject)
    {
        var additions = new List<SubjectMatch>();
        var removals = new List<string>();

        var loweredExistingSubjects = existingSubjects.Select(x => x.ToLowerInvariant());
        foreach (var match in matches)
        {
            var matchName = match.Subject.Name.ToLowerInvariant();
            if (!loweredExistingSubjects.Contains(matchName))
            {
                additions.Add(match);
            }
        }

        var loweredMatchSubjects = matches.Select(x => x.Subject.Name.ToLowerInvariant());
        foreach (var loweredExistingSubject in loweredExistingSubjects)
        {
            if (!loweredMatchSubjects.Contains(loweredExistingSubject))
            {
                if (defaultSubject == null || loweredExistingSubject != defaultSubject.ToLowerInvariant())
                {
                    removals.Add(loweredExistingSubject);
                }
            }
        }

        return (additions, removals);
    }
}