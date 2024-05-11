using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public class Categoriser(
    ISubjectEnricher subjectEnricher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<Categoriser> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICategoriser
{
    public async Task<bool> Categorise(
        Episode episode,
        string[]? ignoredAssociatedSubjects = null,
        string[]? ignoredSubjects = null,
        string? defaultSubject = null)
    {
        var originalSubject = episode.Subjects.ToArray();
        await subjectEnricher.EnrichSubjects(episode, new SubjectEnrichmentOptions(
            ignoredAssociatedSubjects,
            ignoredSubjects,
            defaultSubject));
        var updated = !originalSubject.SequenceEqual(episode.Subjects);
        return updated;
    }
}