namespace RedditPodcastPoster.Common.KnownTerms;

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