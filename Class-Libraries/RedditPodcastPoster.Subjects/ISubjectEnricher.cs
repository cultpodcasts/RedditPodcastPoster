using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectEnricher
{
    Task<(string[] Additions, string[] Removals)> EnrichSubjects(Episode episode,
        SubjectEnrichmentOptions? options = null);
}