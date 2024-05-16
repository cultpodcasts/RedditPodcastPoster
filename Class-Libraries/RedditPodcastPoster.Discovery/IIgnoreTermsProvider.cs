namespace RedditPodcastPoster.Discovery;

public interface IIgnoreTermsProvider
{
    IEnumerable<string> GetIgnoreTerms();
}