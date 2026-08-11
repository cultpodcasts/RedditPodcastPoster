using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;

namespace RedditPodcastPoster.Subjects.Categorisation;

public class Categoriser(
    ISubjectEnricher subjectEnricher,
    ISubjectEnrichmentOptionsFactory subjectEnrichmentOptionsFactory,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<Categoriser> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICategoriser
{
    public async Task<bool> Categorise(Episode episode, Podcast podcast)
    {
        var originalSubject = episode.Subjects.ToArray();
        var options = await subjectEnrichmentOptionsFactory.CreateAsync(podcast, episode);
        await subjectEnricher.EnrichSubjects(episode, options);
        var updated = !originalSubject.SequenceEqual(episode.Subjects);
        return updated;
    }
}
