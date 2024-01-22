namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsProvider(KnownTerms knownTerms) : IKnownTermsProvider
{
    public KnownTerms GetKnownTerms()
    {
        return knownTerms;
    }
}