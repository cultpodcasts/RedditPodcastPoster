using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects.Models;

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
        string[]? ignoredAssociatedSubjects,
        string[]? ignoredSubjects,
        string? defaultSubject,
        string descriptionRegex)
    {
        var originalSubject = episode.Subjects.ToArray();
        var results = await subjectEnricher.EnrichSubjects(episode, new SubjectEnrichmentOptions(
            ignoredAssociatedSubjects,
            ignoredSubjects,
            defaultSubject,
            descriptionRegex));
        var updated = !originalSubject.SequenceEqual(episode.Subjects);
        return updated;
    }
}