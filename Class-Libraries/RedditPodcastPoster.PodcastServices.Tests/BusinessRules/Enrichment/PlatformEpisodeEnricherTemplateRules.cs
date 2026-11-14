using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Enrichment;

/// <summary>
/// Shared enricher template rules: apply-to-context wiring.
/// </summary>
public class PlatformEpisodeEnricherTemplateRules
{
    private readonly DomainTestFixture _fixture = new();

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
                EpisodeServicePresence.SetSpotifyIdentity(e, null);
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

        public PlatformEnrichmentResult ApplyCandidate(
            EnrichmentRequest request,
            EpisodeCandidate candidate,
            EnrichmentContext enrichmentContext) =>
            ApplyResolvedCandidate(request, candidate, enrichmentContext);
    }
}
