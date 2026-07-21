using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Enrichers;

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
        var (additions, removals) = CompareSubjects(
            episode.Subjects,
            episode.RemovedSubjects,
            subjectMatches,
            options?.DefaultSubject);
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
            if (!episode.Subjects.Any() &&
                !string.IsNullOrWhiteSpace(options?.DefaultSubject) &&
                !episode.IsSubjectRemovedByUser(options.DefaultSubject))
            {
                additions.Add(new SubjectMatch(new Subject(options.DefaultSubject), []));
                episode.Subjects = [options.DefaultSubject];
                logger.LogWarning(
                    "Applying default-subject '{defaultSubject}' to episode with title: '{episodeTitle}' ({episodeId}) - no additions.",
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

        if (!string.IsNullOrWhiteSpace(options?.DefaultSubject) &&
            !episode.IsSubjectRemovedByUser(options.DefaultSubject) &&
            !hadSubjects &&
            (!episode.Subjects.Any() || episode.Subjects.All(x => x.StartsWith("_"))))
        {
            if (!episode.Subjects.Contains(options.DefaultSubject, StringComparer.OrdinalIgnoreCase))
            {
                episode.Subjects.Insert(0, options.DefaultSubject);
            }

            if (!additions.Any(x =>
                    string.Equals(x.Subject.Name, options.DefaultSubject, StringComparison.OrdinalIgnoreCase)))
            {
                additions.Insert(0, new SubjectMatch(new Subject(options.DefaultSubject), []));
            }

            logger.LogWarning(
                "Applying default-subject '{defaultSubject}' to episode with title: '{episodeTitle}' ({episodeId}) - fallback.",
                options.DefaultSubject, episode.Title, episode.Id);
        }

        SyncSubjectMatches(episode, subjectMatches);

        return new EnrichSubjectsResult(additions.Select(x => x.Subject.Name).ToArray(), removals.ToArray());
    }

    private static void SyncSubjectMatches(Episode episode, IList<SubjectMatch> subjectMatches)
    {
        episode.Matches.RemoveAll(m => episode.IsSubjectRemovedByUser(m.Subject));

        var episodeSubjects = new HashSet<string>(episode.Subjects, StringComparer.OrdinalIgnoreCase);
        foreach (var match in subjectMatches)
        {
            if (!episodeSubjects.Contains(match.Subject.Name) ||
                episode.IsSubjectRemovedByUser(match.Subject.Name))
            {
                continue;
            }

            var evidence = match.MatchResults.Where(r => r.Source.HasValue).ToArray();
            if (evidence.Length == 0)
            {
                continue;
            }

            episode.Matches.RemoveAll(m =>
                m.Subject.Equals(match.Subject.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var result in evidence)
            {
                episode.Matches.Add(new EpisodeSubjectMatch
                {
                    Subject = match.Subject.Name,
                    Term = result.Term,
                    Source = result.Source!.Value
                });
            }
        }
    }

    private (IList<SubjectMatch>, IList<string>) CompareSubjects(
        IList<string> existingSubjects,
        IList<string> removedSubjects,
        IList<SubjectMatch> matches,
        string? defaultSubject)
    {
        var additions = new List<SubjectMatch>();
        var removals = new List<string>();

        var loweredExistingSubjects = existingSubjects.Select(x => x.ToLowerInvariant());
        foreach (var match in matches)
        {
            var matchName = match.Subject.Name.ToLowerInvariant();
            if (!loweredExistingSubjects.Contains(matchName) &&
                !removedSubjects.Any(x => x.Equals(match.Subject.Name, StringComparison.OrdinalIgnoreCase)))
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