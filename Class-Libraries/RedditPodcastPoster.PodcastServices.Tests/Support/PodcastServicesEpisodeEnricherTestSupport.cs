using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.Tests.Support;

internal static class PodcastServicesEpisodeEnricherTestSupport
{
    public static PodcastServicesEpisodeEnricher CreateEnricher(
        Mock<ISpotifyEpisodeEnricher> spotifyEnricher,
        Mock<IAppleEpisodeEnricher> appleEnricher,
        Mock<IYouTubeEpisodeEnricher> youTubeEnricher,
        TimeSpan? delayedPublicationEvaluationThreshold = null)
    {
        var episodeFilter = new PodcastEpisodeFilter(
            Options.Create(new DelayedYouTubePublication
            {
                EvaluationThreshold = delayedPublicationEvaluationThreshold ?? TimeSpan.FromDays(7)
            }),
            NullLogger<PodcastEpisodeFilter>.Instance);

        return new PodcastServicesEpisodeEnricher(
            appleEnricher.Object,
            spotifyEnricher.Object,
            youTubeEnricher.Object,
            episodeFilter,
            NullLogger<PodcastServicesEpisodeEnricher>.Instance);
    }

    /// <summary>
    /// Spotify enricher mock that applies a real domain patch (adapter + applicator) instead of mutating context only.
    /// </summary>
    public static Mock<ISpotifyEpisodeEnricher> CreateSpotifyEnricherMockApplyingPatch(DomainTestFixture fixture)
    {
        var mock = new Mock<ISpotifyEpisodeEnricher>();
        var applicator = EpisodeDomainTestServices.CreateEnrichmentApplicator();
        var adapter = new SpotifyEpisodeAdapter();

        mock
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((request, _, context) =>
            {
                var input = fixture.CreateSpotifyCatalogueInput();
                applicator.Apply(request.Podcast, request.Episode, adapter.Adapt(input)).ApplyTo(context);
            })
            .Returns(Task.CompletedTask);

        return mock;
    }

    /// <summary>
    /// Apple enricher mock that applies a real domain patch (adapter + applicator) instead of mutating context only.
    /// </summary>
    public static Mock<IAppleEpisodeEnricher> CreateAppleEnricherMockApplyingPatch(DomainTestFixture fixture)
    {
        var mock = new Mock<IAppleEpisodeEnricher>();
        var applicator = EpisodeDomainTestServices.CreateEnrichmentApplicator();
        var adapter = new AppleEpisodeAdapter();

        mock
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((request, _, context) =>
            {
                var input = fixture.CreateAppleCatalogueInput();
                applicator.Apply(request.Podcast, request.Episode, adapter.Adapt(input)).ApplyTo(context);
            })
            .Returns(Task.CompletedTask);

        return mock;
    }

    /// <summary>
    /// YouTube enricher mock that applies a real domain patch (adapter + applicator) instead of mutating context only.
    /// </summary>
    public static Mock<IYouTubeEpisodeEnricher> CreateYouTubeEnricherMockApplyingPatch(DomainTestFixture fixture)
    {
        var mock = new Mock<IYouTubeEpisodeEnricher>();
        var applicator = EpisodeDomainTestServices.CreateEnrichmentApplicator();
        var adapter = new YouTubeEpisodeAdapter();

        mock
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((request, _, context) =>
            {
                var input = fixture.CreateYouTubeCatalogueInput();
                applicator.Apply(request.Podcast, request.Episode, adapter.Adapt(input)).ApplyTo(context);
                context.YouTubeId = input.YouTubeId;
            })
            .Returns(Task.CompletedTask);

        return mock;
    }

    public static Podcast CreateDelayedPublishingPodcast(
        string spotifyShowId,
        string youTubeChannelId,
        TimeSpan publishingDelay) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Delayed-publishing podcast",
            SpotifyId = spotifyShowId,
            ReleaseAuthority = Service.Spotify,
            YouTubeChannelId = youTubeChannelId,
            YouTubePublicationOffset = publishingDelay.Ticks,
            SkipEnrichingFromYouTube = null
        };

    public static void FillYouTubeLink(EnrichmentContext enrichmentContext, string youTubeId)
    {
        enrichmentContext.YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        enrichmentContext.YouTubeId = youTubeId;
    }
}
