using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Enrichers;

public interface ISubjectEnricher
{
    Task<EnrichSubjectsResult> EnrichSubjects(Episode episode, SubjectEnrichmentOptions? options = null);
}