using Moq;
using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Categorisers;
using RedditPodcastPoster.Vimeo.Matching;
namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

internal static class NonPodcastSubmitAdapterResolverSupport
{
    public static INonPodcastServiceAdapterResolver Create(
        IBBCPageMetaDataExtractor? bbcExtractor = null) =>
        new NonPodcastServiceAdapterResolver(
        [
            new BbcNonPodcastServiceAdapter(bbcExtractor ?? Mock.Of<IBBCPageMetaDataExtractor>()),
            new InternetArchiveNonPodcastServiceAdapter(Mock.Of<IInternetArchivePageMetaDataExtractor>()),
            CatalogAdapter(NonPodcastService.Vimeo, ServiceKeys.Vimeo, VimeoUrlMatcher.IsSubmitUrl),
            CatalogAdapter(NonPodcastService.Netflix, ServiceKeys.Netflix, NetflixUrlMatcher.IsSubmitUrl),
            CatalogAdapter(NonPodcastService.AmazonPrime, ServiceKeys.AmazonPrime, AmazonPrimeUrlMatcher.IsSubmitUrl)
        ]);

    private static INonPodcastServiceAdapter CatalogAdapter(
        NonPodcastService service,
        string catalogKey,
        Func<Uri, bool> isSubmitUrl) =>
        new CatalogKeyedNonPodcastServiceAdapter(
            service,
            catalogKey,
            isSubmitUrl,
            isSubmitUrl,
            _ => throw new InvalidOperationException("Extract is not used in submit routing tests."));
}
