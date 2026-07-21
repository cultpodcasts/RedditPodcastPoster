using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Matching;

public interface ISubjectMatcher
{
    Task<IList<SubjectMatch>> MatchSubjects(Episode episode, SubjectEnrichmentOptions? options = null);
}