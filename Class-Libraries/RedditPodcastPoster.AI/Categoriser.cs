using Microsoft.Extensions.Logging;
using RedditPodcastPoster.AI;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;

public class Categoriser(ISubjectEnricher subjectEnricher, ILogger<Categoriser> logger)
    : ICategoriser
{
    public async Task<bool> Categorise(Episode episode, string[]? ignoredTerms = null, string? defaultSubject = null)
    {
        var originalSubject = episode.Subjects.ToArray();
        await subjectEnricher.EnrichSubjects(episode, new SubjectEnrichmentOptions(ignoredTerms, defaultSubject));
        var updated = !originalSubject.SequenceEqual(episode.Subjects);
        return updated;
    }
}