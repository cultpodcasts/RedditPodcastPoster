using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectEnricher(
    ISubjectMatcher subjectMatcher,
    ILogger<ISubjectEnricher> logger)
    : ISubjectEnricher
{
    public async Task<EnrichSubjectsResult> EnrichSubjects(
        Episode episode,
        SubjectEnrichmentOptions? options = null)
    {
        var subjectMatches = await subjectMatcher.MatchSubjects(episode, options);
        var (additions, removals) = CompareSubjects(episode.Subjects, subjectMatches, options?.DefaultSubject);
        var hadSubjects = episode.Subjects.Any();
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

            var terms = string.Join(",",
                additions.Select(x => "'" + x.Subject.Name + "' (" + x.MatchResults.MaxBy(x => x.Matches)?.Term + ")"));
            if (!episode.Subjects.Any() && additions.Count() > 1)
            {
                logger.LogWarning("{method}: {count} - {terms} : '{episodeTitle}' ({episodeId}).",
                    nameof(EnrichSubjects), additions.Count(), terms, episode.Title, episode.Id);
            }
            else
            {
                logger.LogInformation("{method}: {count} - {terms} : '{episodeTitle}' ({episodeId}).",
                    nameof(EnrichSubjects), additions.Count(), terms, episode.Title, episode.Id);
            }


            episode.Subjects.AddRange(additions.Select(x => x.Subject.Name));
        }
        else
        {
            if (!episode.Subjects.Any() && !string.IsNullOrWhiteSpace(options?.DefaultSubject))
            {
                additions.Add(new SubjectMatch(new Subject(options.DefaultSubject), []));
                episode.Subjects = new[] { options.DefaultSubject }.ToList();
                logger.LogWarning(
                    "Applying default-subject '{defaultSubject}' to episode with title: '{episodeTitle}' ({episodeId}).",
                    options.DefaultSubject, episode.Title, episode.Id);
            }
            else if (!episode.Subjects.Any())
            {
                logger.LogError("'No updates: '{episodeTitle}' ({episodeId}).",
                    episode.Title, episode.Id);
            }
        }

        if (removals.Any())
        {
            logger.LogWarning(
                "Redundant: {redundantTerms} : '{episodeTitle}' ({episodeId}).",
                string.Join(",", removals.Select(x => "'" + x + "'")), episode.Title, episode.Id);
        }

        if (!string.IsNullOrWhiteSpace(options.DefaultSubject) && !hadSubjects &&
            (!episode.Subjects.Any() || episode.Subjects.All(x => x.StartsWith("_"))))
        {
            additions.Insert(0, new SubjectMatch(new Subject(options.DefaultSubject), []));
            logger.LogWarning(
                "Applying default-subject '{defaultSubject}' to episode with title: '{episodeTitle}' ({episodeId}).",
                options.DefaultSubject, episode.Title, episode.Id);
        }

        return new EnrichSubjectsResult(additions.Select(x => x.Subject.Name).ToArray(), removals.ToArray());
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