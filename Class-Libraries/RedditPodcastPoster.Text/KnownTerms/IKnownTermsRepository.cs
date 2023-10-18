namespace RedditPodcastPoster.Text.KnownTerms;

public interface IKnownTermsRepository
{
    Task<KnownTerms> Get();
    Task Save(KnownTerms terms);
}