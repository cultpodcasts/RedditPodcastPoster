using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectMatcher
{
    Task<IList<SubjectMatch>> MatchSubjects(Episode episode, SubjectEnrichmentOptions? options = null);
}