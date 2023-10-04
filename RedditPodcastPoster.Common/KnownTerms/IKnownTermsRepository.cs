namespace RedditPodcastPoster.Common.KnownTerms;

public interface IKnownTermsRepository
{
    Task<KnownTerms> Get();
    Task Save(KnownTerms terms);
}