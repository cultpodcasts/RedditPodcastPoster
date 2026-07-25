using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Characterizes <see cref="Common.Episodes.EpisodeProvider"/> indexing discovery orchestration.
/// YouTube release authority must discover via YouTube only — Spotify/Apple catalogue is enrichment, not seed.
/// </summary>
public class EpisodeProviderRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, indexing discovery must not " +
        "merge Apple catalogue episodes as new creates — YouTube alone seeds; Apple attaches via enrichment.")]
    public async Task youtube_release_authority_negative_delay_does_not_merge_apple_catalogue_episodes()
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
        discovered.Should().ContainSingle().Which.Should().BeSameAs(youTubeEpisode);
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, Apple catalogue discovery " +
        "must not run when Spotify indexing is disabled.")]
    public async Task apple_catalogue_merge_pass_does_not_run_when_index_spotify_false()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = CreateYouTubeAuthorityNegativeDelayPodcastWithApple();
        var indexingContext = IsolatedCatalogueMergeContext() with { IndexSpotify = false };

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
        harness.SpotifyHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
        discovered.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, Apple catalogue discovery " +
        "must not run when Spotify URL resolution is bypassed.")]
    public async Task apple_catalogue_merge_pass_does_not_run_when_skip_spotify_url_resolving()
    {
        // Arrange
        var harness = new EpisodeProviderTestHarness();
        var sut = harness.CreateSut();
        var podcast = CreateYouTubeAuthorityNegativeDelayPodcastWithApple();
        var indexingContext = IsolatedCatalogueMergeContext() with { SkipSpotifyUrlResolving = true };

        // Act
        var discovered = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        harness.AppleHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
        harness.SpotifyHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
        discovered.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay and Spotify indexing enabled, " +
        "indexing discovery must not merge Spotify catalogue episodes as new creates.")]
    public async Task youtube_release_authority_negative_delay_does_not_merge_spotify_catalogue_episodes()
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
        discovered.Should().ContainSingle().Which.Should().BeSameAs(youTubeEpisode);
        harness.SpotifyHandler.Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
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
