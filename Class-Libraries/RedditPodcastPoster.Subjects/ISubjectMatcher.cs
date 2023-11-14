using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectMatcher
{
    Task MatchSubject(Episode episode, string[]? ignoredTerms = null);
}