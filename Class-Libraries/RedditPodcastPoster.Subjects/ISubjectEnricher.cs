using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectEnricher
{
    Task<EnrichSubjectsResult> EnrichSubjects(Episode episode, SubjectEnrichmentOptions? options = null);
}