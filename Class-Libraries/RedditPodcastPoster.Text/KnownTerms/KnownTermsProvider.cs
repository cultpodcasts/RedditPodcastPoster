namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsProvider : IKnownTermsProvider
{
    private readonly KnownTerms _knownTerms;

    public KnownTermsProvider(KnownTerms knownTerms)
    {
        _knownTerms = knownTerms;
    }

    public KnownTerms GetKnownTerms()
    {
        return _knownTerms;
    }
}