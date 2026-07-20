using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Categorisers;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// MatchOtherServices must narrow Spotify/Apple lookups with ReleasedSince derived from the
/// authority platform release (Apple −1 day; YouTube ± publishing delay as coded).
/// </summary>
public class UrlCategoriserReleasedSinceRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When MatchOtherServices resolves Spotify from an Apple URL, ReleasedSince is the Apple release minus one day " +
        "because date-scoped Spotify name search must stay near the Apple publish window.")]
    public async Task Apple_authority_sets_spotify_released_since_to_release_minus_one_day()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(14));
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/x/id{_fixture.CreateAppleId()}?i={_fixture.CreateAppleId()}");
        IndexingContext? capturedContext = null;
        var apple = new Mock<IAppleUrlCategoriser>();
        apple
            .Setup(x => x.Resolve(
                It.IsAny<Podcast?>(),
                It.IsAny<IEnumerable<Episode>>(),
                appleUrl,
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateAppleItem(appleUrl, release));

        var spotify = new Mock<ISpotifyUrlCategoriser>();
        spotify
            .Setup(x => x.Resolve(
                It.IsAny<PodcastServiceSearchCriteria>(),
                It.IsAny<Podcast?>(),
                It.IsAny<IndexingContext>()))
            .Callback<PodcastServiceSearchCriteria, Podcast?, IndexingContext>((_, _, ctx) =>
                capturedContext = ctx)
            .ReturnsAsync((ResolvedSpotifyItem?)null);

        var sut = CreateSut(spotify.Object, apple.Object, Mock.Of<IYouTubeUrlCategoriser>());
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = string.Empty);

        // Act
        await sut.Categorise(podcast, appleUrl, new IndexingContext(), matchOtherServices: true);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.ReleasedSince.Should().Be(release.AddDays(-1));
    }

    [Fact(DisplayName =
        "When MatchOtherServices resolves Spotify from a YouTube URL, ReleasedSince is the YouTube release minus the podcast publishing delay " +
        "because audio catalogues lag (or lead) YouTube by the configured offset.")]
    public async Task YouTube_authority_sets_spotify_released_since_to_release_minus_publishing_delay()
    {
        // Arrange
        var delay = TimeSpan.FromHours(9);
        var release = DomainTestFixture.UtcAtTime(-3, TimeSpan.FromHours(11));
        var youTubeId = _fixture.CreateYouTubeId();
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        IndexingContext? capturedContext = null;

        var youTube = new Mock<IYouTubeUrlCategoriser>();
        youTube
            .Setup(x => x.Resolve(
                It.IsAny<Podcast?>(),
                It.IsAny<IList<Episode>>(),
                youTubeUrl,
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateYouTubeItem(youTubeUrl, youTubeId, release));

        var spotify = new Mock<ISpotifyUrlCategoriser>();
        spotify
            .Setup(x => x.Resolve(
                It.IsAny<PodcastServiceSearchCriteria>(),
                It.IsAny<Podcast?>(),
                It.IsAny<IndexingContext>()))
            .Callback<PodcastServiceSearchCriteria, Podcast?, IndexingContext>((_, _, ctx) =>
                capturedContext = ctx)
            .ReturnsAsync((ResolvedSpotifyItem?)null);

        var sut = CreateSut(spotify.Object, Mock.Of<IAppleUrlCategoriser>(), youTube.Object);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePublicationOffset = delay.Ticks;
        });

        // Act
        await sut.Categorise(podcast, youTubeUrl, new IndexingContext(), matchOtherServices: true);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.ReleasedSince.Should().Be(release.Subtract(delay));
    }

    [Fact(DisplayName =
        "When MatchOtherServices resolves YouTube from a Spotify URL, ReleasedSince is the Spotify release plus the podcast publishing delay " +
        "because YouTube often appears after the audio release by the configured offset.")]
    public async Task Spotify_authority_sets_youtube_released_since_to_release_plus_publishing_delay()
    {
        // Arrange
        var delay = TimeSpan.FromHours(6);
        var release = DomainTestFixture.UtcAtTime(-4, TimeSpan.FromHours(8));
        var spotifyId = _fixture.CreateSpotifyId();
        var spotifyUrl = new Uri($"https://open.spotify.com/episode/{spotifyId}");
        IndexingContext? capturedContext = null;

        var spotify = new Mock<ISpotifyUrlCategoriser>();
        spotify
            .Setup(x => x.Resolve(
                It.IsAny<Podcast?>(),
                It.IsAny<IEnumerable<Episode>>(),
                spotifyUrl,
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateSpotifyItem(spotifyUrl, spotifyId, release));

        var youTube = new Mock<IYouTubeUrlCategoriser>();
        youTube
            .Setup(x => x.Resolve(
                It.IsAny<PodcastServiceSearchCriteria>(),
                It.IsAny<Podcast?>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<PodcastServiceSearchCriteria, Podcast?, IList<Episode>, IndexingContext>((_, _, _, ctx) =>
                capturedContext = ctx)
            .ReturnsAsync((ResolvedYouTubeItem?)null);

        var apple = new Mock<IAppleUrlCategoriser>();
        apple
            .Setup(x => x.Resolve(
                It.IsAny<PodcastServiceSearchCriteria>(),
                It.IsAny<Podcast?>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync((ResolvedAppleItem?)null);

        var sut = CreateSut(spotify.Object, apple.Object, youTube.Object);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.YouTubeChannelId = string.Empty;
            p.AppleId = null;
            p.YouTubePublicationOffset = delay.Ticks;
        });

        // Act
        await sut.Categorise(podcast, spotifyUrl, new IndexingContext(), matchOtherServices: true);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.ReleasedSince.Should().Be(release.Add(delay));
    }

    private static UrlCategoriser CreateSut(
        ISpotifyUrlCategoriser spotify,
        IAppleUrlCategoriser apple,
        IYouTubeUrlCategoriser youTube) =>
        new(
            spotify,
            apple,
            youTube,
            new InMemoryEpisodeRepository(),
            Mock.Of<INonPodcastServiceCategoriser>(),
            NullLogger<UrlCategoriser>.Instance);

    private ResolvedAppleItem CreateAppleItem(Uri url, DateTime release) =>
        new(
            showId: _fixture.CreateAppleId(),
            episodeId: _fixture.CreateAppleId(),
            showName: _fixture.CreateTitle(),
            showDescription: _fixture.CreateTitle(),
            publisher: _fixture.CreateTitle(),
            episodeTitle: _fixture.CreateTitle(),
            episodeDescription: _fixture.CreateTitle(),
            release: release,
            duration: _fixture.CreateDuration(),
            url: url,
            @explicit: false,
            image: null);

    private ResolvedYouTubeItem CreateYouTubeItem(Uri url, string episodeId, DateTime release) =>
        new(
            showId: _fixture.CreateYouTubeChannelId(),
            episodeId: episodeId,
            showName: _fixture.CreateTitle(),
            showDescription: _fixture.CreateTitle(),
            publisher: _fixture.CreateTitle(),
            episodeTitle: _fixture.CreateTitle(),
            episodeDescription: _fixture.CreateTitle(),
            release: release,
            duration: _fixture.CreateDuration(),
            url: url,
            @explicit: false,
            image: null,
            playlistId: null);

    private ResolvedSpotifyItem CreateSpotifyItem(Uri url, string episodeId, DateTime release) =>
        new(
            showId: _fixture.CreateSpotifyId(),
            episodeId: episodeId,
            showName: _fixture.CreateTitle(),
            showDescription: _fixture.CreateTitle(),
            publisher: _fixture.CreateTitle(),
            episodeTitle: _fixture.CreateTitle(),
            episodeDescription: _fixture.CreateTitle(),
            release: release,
            duration: _fixture.CreateDuration(),
            url: url,
            @explicit: false,
            image: null);
}
