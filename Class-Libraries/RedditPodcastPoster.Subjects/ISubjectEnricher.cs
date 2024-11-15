using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectEnricher
{
    Task<(string[], string[])> EnrichSubjects(Episode episode, SubjectEnrichmentOptions? options = null);
}