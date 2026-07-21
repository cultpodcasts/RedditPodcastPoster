namespace RedditPodcastPoster.Discovery.Providers;

public interface IIgnoreTermsProvider
{
    IEnumerable<string> GetIgnoreTerms();
}