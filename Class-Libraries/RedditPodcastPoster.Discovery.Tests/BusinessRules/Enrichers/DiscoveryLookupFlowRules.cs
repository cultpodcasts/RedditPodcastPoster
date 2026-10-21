using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Discovery.Enrichers;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules.Enrichers;

/// <summary>Discovery enrichers mark their catalogue lookups as the Discovery flow (docs/catalogue-pagination.md).</summary>
public class DiscoveryLookupFlowRules
{
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName = "SpotifyEnricher.Enrich sets IndexingContext.LookupFlow to Discovery, so old episodes are searched before any bounded walk.")]
    public async Task Spotify_enricher_sets_discovery_flow()
    {
        // Arrange
        var context = new IndexingContext();
        var sut = _mocker.CreateInstance<SpotifyEnricher>();

        // Act
        await sut.Enrich([], context);

        // Assert
        context.LookupFlow.Should().Be(EpisodeLookupFlow.Discovery);
    }

    [Fact(DisplayName = "AppleEnricher.Enrich sets IndexingContext.LookupFlow to Discovery, so old episodes are searched before any bounded walk.")]
    public async Task Apple_enricher_sets_discovery_flow()
    {
        // Arrange
        var context = new IndexingContext();
        var sut = _mocker.CreateInstance<AppleEnricher>();

        // Act
        await sut.Enrich([], context);

        // Assert
        context.LookupFlow.Should().Be(EpisodeLookupFlow.Discovery);
    }
}
