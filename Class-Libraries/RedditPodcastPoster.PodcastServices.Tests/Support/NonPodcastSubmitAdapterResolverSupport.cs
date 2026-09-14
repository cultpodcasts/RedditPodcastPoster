using Moq;
using RedditPodcastPoster.AmazonPrime.Extractors;
using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix.Extractors;
using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Categorisers;
using RedditPodcastPoster.Vimeo.Extractors;
using RedditPodcastPoster.Vimeo.Matching;

namespace RedditPodcastPoster.PodcastServices.Tests.Support;

internal static class NonPodcastSubmitAdapterResolverSupport
{
    public static INonPodcastServiceAdapterResolver Create(
        IBBCPageMetaDataExtractor bbc,
        IInternetArchivePageMetaDataExtractor archive,
        IVimeoMetaDataExtractor vimeo,
        INetflixPageMetaDataExtractor netflix,
        IAmazonPrimePageMetaDataExtractor prime) =>
        new NonPodcastServiceAdapterResolver(
        [
            new BbcNonPodcastServiceAdapter(bbc),
            new InternetArchiveNonPodcastServiceAdapter(archive),
            new CatalogKeyedNonPodcastServiceAdapter(
                StreamingService.Vimeo,
                VimeoUrlMatcher.IsSubmitUrl,
                VimeoUrlMatcher.IsSubmitUrl,
                vimeo.GetMetaData),
            new CatalogKeyedNonPodcastServiceAdapter(
                StreamingService.Netflix,
                NetflixUrlMatcher.IsSubmitUrl,
                NetflixUrlMatcher.IsSubmitUrl,
                netflix.GetMetaData),
            new CatalogKeyedNonPodcastServiceAdapter(
                StreamingService.AmazonPrime,
                AmazonPrimeUrlMatcher.IsSubmitUrl,
                AmazonPrimeUrlMatcher.IsSubmitUrl,
                prime.GetMetaData)
        ]);

    public static INonPodcastServiceAdapterResolver CreateMocks() =>
        Create(
            Mock.Of<IBBCPageMetaDataExtractor>(),
            Mock.Of<IInternetArchivePageMetaDataExtractor>(),
            Mock.Of<IVimeoMetaDataExtractor>(),
            Mock.Of<INetflixPageMetaDataExtractor>(),
            Mock.Of<IAmazonPrimePageMetaDataExtractor>());
}
