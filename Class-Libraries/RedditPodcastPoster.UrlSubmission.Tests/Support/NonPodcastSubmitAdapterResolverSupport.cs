// pragma: allowlist secret
using Moq;
using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Models; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Categorisers; // pragma: allowlist secret
using RedditPodcastPoster.Vimeo.Matching;
using RedditPodcastPoster.BitChute.Matching; // pragma: allowlist secret

namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

internal static class NonPodcastSubmitAdapterResolverSupport // pragma: allowlist secret
{
    public static INonPodcastServiceAdapterResolver Create( // pragma: allowlist secret
        IBBCPageMetaDataExtractor? bbcExtractor = null,
        Func<Uri, Task<NonPodcastServiceItemMetaData>>? vimeoExtract = null, // pragma: allowlist secret
        Func<Uri, Task<NonPodcastServiceItemMetaData>>? netflixExtract = null, // pragma: allowlist secret
        Func<Uri, Task<NonPodcastServiceItemMetaData>>? primeExtract = null, // pragma: allowlist secret
        IInternetArchivePageMetaDataExtractor? archiveExtractor = null) =>
        new NonPodcastServiceAdapterResolver( // pragma: allowlist secret
        [
            new BbcNonPodcastServiceAdapter(bbcExtractor ?? Mock.Of<IBBCPageMetaDataExtractor>()), // pragma: allowlist secret
            new InternetArchiveNonPodcastServiceAdapter( // pragma: allowlist secret
                archiveExtractor ?? Mock.Of<IInternetArchivePageMetaDataExtractor>()),
            CatalogAdapter(NonPodcastService.Vimeo, ServiceKeys.Vimeo, VimeoUrlMatcher.IsSubmitUrl, vimeoExtract), // pragma: allowlist secret
            CatalogAdapter(NonPodcastService.BitChute, ServiceKeys.BitChute, BitChuteUrlMatcher.IsSubmitUrl), // pragma: allowlist secret
            CatalogAdapter(NonPodcastService.Netflix, ServiceKeys.Netflix, NetflixUrlMatcher.IsSubmitUrl, netflixExtract), // pragma: allowlist secret
            CatalogAdapter(NonPodcastService.AmazonPrime, ServiceKeys.AmazonPrime, AmazonPrimeUrlMatcher.IsSubmitUrl, primeExtract) // pragma: allowlist secret
        ]);

    private static INonPodcastServiceAdapter CatalogAdapter( // pragma: allowlist secret
        NonPodcastService service, // pragma: allowlist secret
        string catalogKey,
        Func<Uri, bool> isSubmitUrl,
        Func<Uri, Task<NonPodcastServiceItemMetaData>>? extract = null) => // pragma: allowlist secret
        new CatalogKeyedNonPodcastServiceAdapter( // pragma: allowlist secret
            service,
            catalogKey,
            isSubmitUrl,
            isSubmitUrl,
            extract ?? (_ => throw new InvalidOperationException("Extract is not used in submit routing tests.")));
}
