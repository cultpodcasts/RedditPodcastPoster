using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectEnricher(
    ISubjectMatcher subjectMatcher,
    ILogger<ISubjectEnricher> logger)
    : ISubjectEnricher
{
    public async Task<(string[] Additions, string[] Removals)> EnrichSubjects(Episode episode,
        SubjectEnrichmentOptions? options = null)
    {
        var subjectMatches = await subjectMatcher.MatchSubjects(episode, options);
        var (additions, removals) = CompareSubjects(episode.Subjects, subjectMatches, options?.DefaultSubject);

        if (additions.Any())
        {
            if (options != null)
            {
                var defaultSubjectItem = additions.SingleOrDefault(x => x.Subject.Name == options.DefaultSubject);
                if (defaultSubjectItem != null)
                {
                    var defaultSubjectIndex = additions.IndexOf(defaultSubjectItem);
                    additions.RemoveAt(defaultSubjectIndex);
                    additions.Insert(0, defaultSubjectItem);
                }
            }

            var message =
                $"{additions.Count()} - {string.Join(",", additions.Select(x => "'" + x.Subject.Name + "' (" + x.MatchResults.MaxBy(x => x.Matches)?.Term + ")"))} : '{episode.Title}'.";
            if (!episode.Subjects.Any() && additions.Count() > 1)
            {
                logger.LogWarning(message);
            }
            else
            {
                logger.LogInformation(message);
            }


            episode.Subjects.AddRange(additions.Select(x => x.Subject.Name));
        }
        else
        {
            if (!episode.Subjects.Any() && !string.IsNullOrWhiteSpace(options?.DefaultSubject))
            {
                episode.Subjects = new[] {options.DefaultSubject}.ToList();
                logger.LogWarning(
                    $"Applying default-subject '{options.DefaultSubject}' to episode with title: '{episode.Title}'.");
            }
            else if (!episode.Subjects.Any())
            {
                logger.LogError($"'No updates: '{episode.Title}'.");
            }
        }

        if (removals.Any())
        {
            logger.LogWarning(
                $"Redundant: {string.Join(",", removals.Select(x => "'" + x + "'"))} : '{episode.Title}'.");
        }

        return (additions.Select(x => x.Subject.Name).ToArray(), removals.ToArray());
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