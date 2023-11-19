using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectEnricher
{
    Task EnrichSubjects(Episode episode, SubjectEnrichmentOptions? options = null);
}