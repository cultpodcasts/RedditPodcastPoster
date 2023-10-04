namespace RedditPodcastPoster.Common.KnownTerms;

public interface IKnownTermsProviderFactory
{
    Task<IKnownTermsProvider> Create();
}