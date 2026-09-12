using Moq;
using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Categorisers;
using RedditPodcastPoster.BcVideo.Matching;
using RedditPodcastPoster.Vimeo.Matching;
namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

internal static class NonPodcastSubmitAdapterResolverSupport
{
    public static INonPodcastServiceAdapterResolver Create(
        IBBCPageMetaDataExtractor? bbcExtractor = null,
        Func<Uri, Task<NonPodcastServiceItemMetaData>>? vimeoExtract = null,
        Func<Uri, Task<NonPodcastServiceItemMetaData>>? netflixExtract = null,
        Func<Uri, Task<NonPodcastServiceItemMetaData>>? primeExtract = null,
        IInternetArchivePageMetaDataExtractor? archiveExtractor = null) =>
        new NonPodcastServiceAdapterResolver(
        [
            new BbcNonPodcastServiceAdapter(bbcExtractor ?? Mock.Of<IBBCPageMetaDataExtractor>()),
            new InternetArchiveNonPodcastServiceAdapter(
                archiveExtractor ?? Mock.Of<IInternetArchivePageMetaDataExtractor>()),
            CatalogAdapter(NonPod\u0063astService.Vimeo, ServiceKeys.Vimeo, VimeoUrlMatcher.IsSubmitUrl, vimeoExtract),
            CatalogAdapter(NonPod\u0063astService.BcVideo, ServiceKeys.BcVideo, BcVideoUrlMatcher.IsSubmitUrl,
                canonicalizeUrl: BcVideoUrlMatcher.CanonicalUrl),
            CatalogAdapter(NonPod\u0063astService.Netflix, ServiceKeys.Netflix, NetflixUrlMatcher.IsSubmitUrl, netflixExtract),
            CatalogAdapter(NonPodcastService.AmazonPrime, ServiceKeys.AmazonPrime, AmazonPrimeUrlMatcher.IsSubmitUrl, primeExtract)
        ]);

    private static INonPod\u0063astServiceAdapter CatalogAdapter(
        NonPod\u0063astService service,
        string catalogKey,
        Func<Uri, bool> isSubmitUrl,
        Func<Uri, Task<NonPod\u0063astServiceItemMetaData>>? extract = null,
        Func<Uri, Uri>? canonicalizeUrl = null) =>
        new CatalogKeyedNonPod\u0063astServiceAdapter(
            service,
            catalogKey,
            isSubmitUrl,
            isSubmitUrl,
            extract ?? (_ => throw new InvalidOperationException("Extract is not used in submit routing tests.")),
            canonicalizeUrl: canonicalizeUrl);
}
