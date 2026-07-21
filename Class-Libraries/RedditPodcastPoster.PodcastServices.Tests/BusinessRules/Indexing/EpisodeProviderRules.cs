using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Characterizes <see cref="Common.Episodes.EpisodeProvider"/> indexing discovery orchestration,
/// including YouTube release authority catalogue merge passes for cross-platform matching.
/// </summary>
public class EpisodeProviderRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay and a configured Apple id, " +
        "indexing discovery must merge Apple catalogue episodes into the discovered set for cross-platform matching.")]
    public async Task youtube_release_authority_negative_delay_merges_apple_catalogue_episodes()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = CreateYouTubeAuthorityNegativeDelayPodcastWithApple();
        var youTubeEpisode = _fixture.CreateYouTubeCatalogueEpisode();
        var appleEpisode = _fixture.CreateAppleCatalogueEpisode();
        var indexingContext = CatalogueMergeWithYouTubeDiscoveryContext();

        harness.YouTubeHandler
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([youTubeEpisode], Handled: true));
        harness.AppleHandler
            .Setup(x => x.GetEpisodes(podcast, indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([appleEpisode], Handled: true));

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        discovered.Should().Contain(youTubeEpisode);
        discovered.Should().Contain(appleEpisode);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, Apple catalogue discovery " +
        "must run even when Spotify indexing is disabled â€” Apple merge supports cross-platform matching " +
        "independently of Spotify URL resolution.")]
    public async Task apple_catalogue_merge_pass_runs_when_index_spotify_false()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = CreateYouTubeAuthorityNegativeDelayPodcastWithApple();
        var appleEpisode = _fixture.CreateAppleCatalogueEpisode();
        var indexingContext = IsolatedCatalogueMergeContext() with { IndexSpotify = false };

        harness.AppleHandler
            .Setup(x => x.GetEpisodes(podcast, indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([appleEpisode], Handled: true));

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(podcast, indexingContext),
            Times.Once);
        harness.SpotifyHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
        discovered.Should().ContainSingle().Which.Should().BeSameAs(appleEpisode);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, Apple catalogue discovery " +
        "must still run when Spotify URL resolution is bypassed.")]
    public async Task apple_catalogue_merge_pass_runs_when_skip_spotify_url_resolving()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = CreateYouTubeAuthorityNegativeDelayPodcastWithApple();
        var appleEpisode = _fixture.CreateAppleCatalogueEpisode();
        var indexingContext = IsolatedCatalogueMergeContext() with { SkipSpotifyUrlResolving = true };

        harness.AppleHandler
            .Setup(x => x.GetEpisodes(podcast, indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([appleEpisode], Handled: true));

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(podcast, indexingContext),
            Times.Once);
        harness.SpotifyHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
        discovered.Should().ContainSingle().Which.Should().BeSameAs(appleEpisode);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay and Spotify indexing enabled, " +
        "indexing discovery must merge Spotify catalogue episodes alongside YouTube discovery.")]
    public async Task youtube_release_authority_negative_delay_merges_spotify_catalogue_episodes()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeEpisode = _fixture.CreateYouTubeCatalogueEpisode();
        var spotifyEpisode = _fixture.CreateSpotifyCatalogueEpisode();
        var indexingContext = CatalogueMergeWithYouTubeDiscoveryContext();

        harness.YouTubeHandler
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([youTubeEpisode], Handled: true));
        harness.SpotifyHandler
            .Setup(x => x.GetEpisodes(podcast, indexingContext))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([spotifyEpisode], Handled: true));

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        discovered.Should().Contain(youTubeEpisode);
        discovered.Should().Contain(spotifyEpisode);
    }

    [Fact(DisplayName =
        "When YouTube publishing delay is zero, EpisodeProvider must not run the Apple catalogue merge pass " +
        "for YouTube release authority podcasts.")]
    public async Task zero_delay_skips_apple_catalogue_merge_pass()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            youTubePublicationOffsetTicks: 0);
        podcast.AppleId = _fixture.CreateAppleId();
        var indexingContext = IsolatedCatalogueMergeContext();

        // Act
        await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the podcast has no Apple id configured, EpisodeProvider must not run the Apple catalogue merge pass " +
        "for YouTube release authority negative-delay podcasts.")]
    public async Task missing_apple_id_skips_apple_catalogue_merge_pass()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.AppleId = null;
        var indexingContext = IsolatedCatalogueMergeContext();

        // Act
        await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    private Podcast CreateYouTubeAuthorityNegativeDelayPodcastWithApple()
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.AppleId = _fixture.CreateAppleId();
        return podcast;
    }

    private static IndexingContext IsolatedCatalogueMergeContext() =>
        new()
        {
            SkipYouTubeUrlResolving = true,
            SkipShortEpisodes = false
        };

    private static IndexingContext CatalogueMergeWithYouTubeDiscoveryContext() =>
        new()
        {
            SkipYouTubeUrlResolving = false,
            SkipShortEpisodes = false
        };
}
