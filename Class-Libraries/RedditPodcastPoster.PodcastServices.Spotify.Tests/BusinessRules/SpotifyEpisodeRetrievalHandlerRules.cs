using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules;

/// <summary>
/// Indexer Spotify retrieval must not call the provider when the podcast has no Spotify show id.
/// </summary>
public class SpotifyEpisodeRetrievalHandlerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the podcast has an empty SpotifyId, GetEpisodes returns not-handled with no episodes and does not call the provider " +
        "because hourly indexing only fetches catalogue rows for shows with a known Spotify id.")]
    public async Task Empty_spotify_id_does_not_call_provider()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = string.Empty);
        var provider = new Mock<ISpotifyEpisodeProvider>(MockBehavior.Strict);
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(podcast, new IndexingContext(SkipPodcastDiscovery: true));

        // Assert
        result.Handled.Should().BeFalse();
        result.Episodes.Should().BeEmpty();
        provider.Verify(
            x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the provider reports ExpensiveQueryFound, the handler sets SpotifyEpisodesQueryIsExpensive on the podcast " +
        "because subsequent indexer passes must skip expensive pagination for that show.")]
    public async Task Expensive_query_found_sets_podcast_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.SpotifyEpisodesQueryIsExpensive = false;
        });
        var provider = new Mock<ISpotifyEpisodeProvider>();
        provider
            .Setup(x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetEpisodesResponse([], ExpensiveQueryFound: true));
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(podcast, new IndexingContext(SkipPodcastDiscovery: true));

        // Assert
        result.Handled.Should().BeTrue();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
        provider.Verify(
            x => x.GetEpisodes(
                It.Is<GetEpisodesRequest>(r =>
                    r.SpotifyPodcastId.PodcastId == podcast.SpotifyId &&
                    r.HasExpensiveSpotifyEpisodesQuery == false),
                It.IsAny<IndexingContext>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When SkipSpotifyUrlResolving is set, GetEpisodes still calls the provider but returns Handled=false even if episodes are returned " +
        "because rate-limit recovery must not mark Spotify as fully handled for the indexer pass.")]
    public async Task Skip_spotify_url_resolving_returns_not_handled()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = _fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var provider = new Mock<ISpotifyEpisodeProvider>();
        provider
            .Setup(x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetEpisodesResponse([episode], ExpensiveQueryFound: false));
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(
            podcast,
            new IndexingContext { SkipSpotifyUrlResolving = true });

        // Assert
        result.Handled.Should().BeFalse();
        result.Episodes.Should().ContainSingle();
        provider.Verify(
            x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()),
            Times.Once);
    }
}
