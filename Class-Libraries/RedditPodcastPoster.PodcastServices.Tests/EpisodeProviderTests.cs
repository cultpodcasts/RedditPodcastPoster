using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Tests;

/// <summary>
/// Unit tests for <see cref="Common.Episodes.EpisodeProvider"/> handler orchestration gates.
/// Business-rule outcomes live in <see cref="BusinessRules.Indexing.EpisodeProviderRules"/>.
/// </summary>
public class EpisodeProviderTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact]
    public async Task GetEpisodes_does_not_run_apple_negative_delay_merge_pass_when_release_authority_is_spotify()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();
        podcast.YouTubePublicationOffset = DomainTestFixture.DefaultNegativeYouTubePublishingDelayTicks;
        var indexingContext = new IndexingContext { SkipYouTubeUrlResolving = true };
        var spotifyEpisode = _fixture.CreateSpotifyCatalogueEpisode();

        harness.SpotifyHandler
            .Setup(x => x.GetEpisodes(podcast, indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([spotifyEpisode], Handled: true));

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert â€” Spotify-primary discovery handles the podcast; Apple merge pass must not run
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
        discovered.Should().ContainSingle().Which.Should().BeSameAs(spotifyEpisode);
    }

    [Fact]
    public async Task GetEpisodes_skips_spotify_merge_pass_when_skip_spotify_url_resolving()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var indexingContext = new IndexingContext
        {
            SkipYouTubeUrlResolving = true,
            SkipSpotifyUrlResolving = true
        };

        // Act
        await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        harness.SpotifyHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEpisodes_does_not_append_apple_episodes_when_apple_handler_returns_empty()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.AppleId = _fixture.CreateAppleId();
        var indexingContext = new IndexingContext { SkipYouTubeUrlResolving = true };

        harness.AppleHandler
            .Setup(x => x.GetEpisodes(podcast, indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([], Handled: true));

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        discovered.Should().BeEmpty();
    }
}
