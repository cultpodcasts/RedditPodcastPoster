using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Enrichment;

/// <summary>
/// Shared enricher template rules â€” delayed-publishing bypass and apply-to-context wiring.
/// </summary>
public class PlatformEpisodeEnricherTemplateRules
{
    private static readonly TimeSpan PublishingDelay = TimeSpan.FromDays(1);

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When an episode is outside the delayed-publishing window, the shared enricher template " +
        "does not bypass platform enrichment.")]
    public void template_does_not_bypass_enrichment_outside_delayed_publishing_window()
    {
        // Arrange
        var podcast = PodcastServicesEpisodeEnricherTestSupport.CreateDelayedPublishingPodcast(
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeChannelId(),
            PublishingDelay);
        var release = DomainTestFixture.UtcDateDaysAgo(30);
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(release)
            .WithDuration(_fixture.CreateDuration()));
        var request = new EnrichmentRequest(podcast, [episode], episode);
        var enricher = new TestPlatformEpisodeEnricher(EpisodeDomainTestServices.CreateEnrichmentApplicator());

        // Act
        var bypassed = enricher.TryBypass(request, NullLogger.Instance);

        // Assert
        bypassed.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an episode is still inside the delayed-publishing window, the shared enricher template " +
        "bypasses platform enrichment because audio is not yet due on YouTube.")]
    public void template_bypasses_enrichment_inside_delayed_publishing_window()
    {
        // Arrange
        var podcast = PodcastServicesEpisodeEnricherTestSupport.CreateDelayedPublishingPodcast(
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeChannelId(),
            PublishingDelay);
        var release = DomainTestFixture.SpotifyCatalogueReleaseStillInsideDelayedPublishingWindow(PublishingDelay);
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(release)
            .WithDuration(_fixture.CreateDuration()));
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;
        var request = new EnrichmentRequest(podcast, [episode], episode);
        var enricher = new TestPlatformEpisodeEnricher(EpisodeDomainTestServices.CreateEnrichmentApplicator());

        // Act
        var bypassed = enricher.TryBypass(request, NullLogger.Instance);

        // Assert
        bypassed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When ApplyResolvedCandidate applies a Spotify catalogue candidate, the enrichment context " +
        "records the Spotify URL update and the episode receives the platform link via the applicator.")]
    public void apply_resolved_candidate_updates_enrichment_context_and_episode_state()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var candidate = new SpotifyEpisodeAdapter().Adapt(spotifyInput);
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.SpotifyId = string.Empty;
                e.Urls = new ServiceUrls();
            })
            .Create();
        var request = new EnrichmentRequest(podcast, [episode], episode);
        var enrichmentContext = new EnrichmentContext();
        var enricher = new TestPlatformEpisodeEnricher(EpisodeDomainTestServices.CreateEnrichmentApplicator());
        var expected = EpisodeExpectation.From(episode)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl, spotifyInput.Image);

        // Act
        var result = enricher.ApplyCandidate(request, candidate, enrichmentContext);

        // Assert
        result.Updated.Should().BeTrue();
        result.Service.Should().Be(Service.Spotify);
        enrichmentContext.SpotifyUrlUpdated.Should().BeTrue();
        episode.ShouldMatchExpectation(expected);
    }

    private sealed class TestPlatformEpisodeEnricher(IPlatformEnrichmentApplicator enrichmentApplicator)
        : PlatformEpisodeEnricherTemplate(enrichmentApplicator)
    {
        public bool TryBypass(EnrichmentRequest request, ILogger logger) =>
            IsBypassedByDelayedYouTubePublishing(request, "Test", logger);

        public PlatformEnrichmentResult ApplyCandidate(
            EnrichmentRequest request,
            EpisodeCandidate candidate,
            EnrichmentContext enrichmentContext) =>
            ApplyResolvedCandidate(request, candidate, enrichmentContext);
    }
}
