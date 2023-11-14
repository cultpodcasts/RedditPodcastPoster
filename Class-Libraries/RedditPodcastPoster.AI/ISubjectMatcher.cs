using RedditPodcastPoster.Models;

public interface ISubjectMatcher
{
    Task MatchSubject(Episode episode, string[]? ignoredTerms = null);
}