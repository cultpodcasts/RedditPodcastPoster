namespace RedditPodcastPoster.Text.KnownTerms;

public interface IKnownTermsProviderFactory
{
    Task<IKnownTermsProvider> Create();
}