using AutoFixture;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Services;

public class SearchResultFinderTests
{
    private static readonly TimeSpan DefaultEpisodeLength = TimeSpan.FromHours(1);
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IYouTubeSearchResultFinder Sut => _mocker.CreateInstance<YouTubeSearchResultFinder>();

    public SearchResultFinderTests()
    {
        _mocker.Use(EpisodeDomainTestServices.CreatePlatformMatcher());
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IYouTubeServiceWrapper _, IEnumerable<string> videoIds, IndexingContext _, bool _, bool _) =>
                videoIds.Select(CreateVideoWithMatchingDuration).ToList());
    }

    private static Google.Apis.YouTube.v3.Data.Video CreateVideoWithMatchingDuration(string videoId) =>
        new()
        {
            Id = videoId,
            ContentDetails = new VideoContentDetails { Duration = "PT1H0M0S" },
            Snippet = new VideoSnippet { LiveBroadcastContent = "none" }
        };

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task FindMatchingYouTubeVideo_WithInAccurateReleaseTimeAndUnmatchedVideoCloserToMidnight_IsCorrect(
        long youTubePublishDelayTicks)
    {
        // arrange
        var expectedTitle = "Matching Episode";
        var today = DateTime.UtcNow.Date;
        var episode = _fixture
            .Build<RedditPodcastPoster.Models.Episode>()
            .With(x => x.Title, expectedTitle)
            .With(x => x.Release, today)
            .With(x => x.Length, DefaultEpisodeLength)
            .With(x => x.AppleId, (long?) null)
            .With(x => x.Urls, _fixture.Build<ServiceUrls>().With(x => x.Apple, (Uri?) null).Create())
            .Create();
        var expected = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.Title, expectedTitle)
                .With(x => x.PublishedAtDateTimeOffset, today.AddTicks(youTubePublishDelayTicks))
                .Create())
            .Create();
        var incorrectResult = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.PublishedAtDateTimeOffset, today)
                .Create())
            .Create();
        // act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            new List<SearchResult> {expected, incorrectResult},
            TimeSpan.FromTicks(youTubePublishDelayTicks),
            new IndexingContext());
        // assert
        result?.SearchResult.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task FindMatchingYouTubeVideo_WithAccurateReleaseTimeAndUnmatchedVideoCloserToMidnight_IsCorrect(
        long youTubePublishDelayTicks)
    {
        // arrange
        var expectedTitle = "Matching Episode";
        var today = DateTime.UtcNow.Date;
        var release = DateTime.UtcNow.Date.AddHours(17);
        var episode = _fixture
            .Build<RedditPodcastPoster.Models.Episode>()
            .With(x => x.Title, "Episode-title")
            .With(x => x.Title, expectedTitle)
            .With(x => x.Release, release)
            .With(x => x.Length, DefaultEpisodeLength)
            .Create();
        var expected = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.Title, expectedTitle)
                .With(x => x.PublishedAtDateTimeOffset, release.AddMinutes(5).AddTicks(youTubePublishDelayTicks))
                .Create())
            .Create();
        var incorrectResult = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.PublishedAtDateTimeOffset, today)
                .Create())
            .Create();
        // act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            new List<SearchResult> {expected, incorrectResult},
            TimeSpan.FromTicks(youTubePublishDelayTicks),
            new IndexingContext());
        // assert
        result?.SearchResult.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task
        FindMatchingYouTubeVideo_WithAccurateReleaseTimeAndMatchingVideoReleasedOnYouTubeBeforeAudioAndUnmatchedVideoCloserToMidnight_IsCorrect(
            long youTubePublishDelayTicks)
    {
        // arrange
        var expectedTitle = "Matching Episode";
        var today = DateTime.UtcNow.Date;
        var release = DateTime.UtcNow.Date.AddHours(17);
        var episode = _fixture
            .Build<RedditPodcastPoster.Models.Episode>()
            .With(x => x.Title, "Episode-title")
            .With(x => x.Title, expectedTitle)
            .With(x => x.Release, release)
            .With(x => x.Length, DefaultEpisodeLength)
            .Create();
        var expected = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.Title, expectedTitle)
                .With(x => x.PublishedAtDateTimeOffset, release.AddMinutes(-5))
                .Create())
            .Create();
        var incorrectResult = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.PublishedAtDateTimeOffset, today)
                .Create())
            .Create();
        // act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            new List<SearchResult> {expected, incorrectResult},
            TimeSpan.FromTicks(youTubePublishDelayTicks),
            new IndexingContext());
        // assert
        result?.SearchResult.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task FindMatchingYouTubeVideo_WithMatchingEpisodeNumber_IsCorrect(long youTubePublishDelayTicks)
    {
        // arrange
        var episodeNumber = _fixture.Create<int>();
        var today = DateTime.UtcNow.Date;
        var release = DateTime.UtcNow.Date.AddHours(17);
        var episode = _fixture
            .Build<RedditPodcastPoster.Models.Episode>()
            .With(x => x.Title, $"Prefix-A {episodeNumber} Suffix-A")
            .With(x => x.Release, release)
            .With(x => x.Length, DefaultEpisodeLength)
            .Create();
        var expected = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.Title, $"Prefix-B {episodeNumber} Suffix-B")
                .With(x => x.PublishedAtDateTimeOffset, release.AddMinutes(-5))
                .Create())
            .Create();
        var incorrectResult = _fixture
            .Build<SearchResult>()
            .With(x => x.Snippet, _fixture
                .Build<SearchResultSnippet>()
                .With(x => x.PublishedAtDateTimeOffset, today)
                .Create())
            .Create();
        // act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            new List<SearchResult> {expected, incorrectResult},
            TimeSpan.FromTicks(youTubePublishDelayTicks),
            new IndexingContext());
        // assert
        result?.SearchResult.Should().Be(expected);
    }
}
