using System.Xml;
using FluentAssertions;
using FuzzySharp;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Finders;

/// <summary>
/// Search-result finder characterization: exact-title matching, publish-delay delegation to
/// <see cref="RedditPodcastPoster.Episodes.Matching.IEpisodePlatformMatcher.IsCatalogueMatch"/>,
/// local fuzzy-title closeness (FuzzySharp â‰¥ 70), and wrapper-specific duration gates.
/// </summary>
public class SearchResultFinderCatalogueWrapperRules
{
    private const int MinFuzzyTitleScore = 70;

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public SearchResultFinderCatalogueWrapperRules()
    {
        _mocker.Use(EpisodeDomainTestServices.CreatePlatformMatcher());
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((
                IYouTubeServiceWrapper _,
                IEnumerable<string> videoIds,
                IndexingContext _,
                bool _,
                bool _) =>
                videoIds.Select(id => CreateCompletedVideo(id, TimeSpan.FromHours(1))).ToList());
    }

    public static TheoryData<FuzzyTitleVariantStrategy> AllFuzzyVariantStrategies() =>
        new()
        {
            FuzzyTitleVariantStrategy.ReplaceWord,
            FuzzyTitleVariantStrategy.DropWord,
            FuzzyTitleVariantStrategy.AddFillerWord,
            FuzzyTitleVariantStrategy.SwapAdjacentWords
        };

    private IYouTubeSearchResultFinder Sut => _mocker.CreateInstance<YouTubeSearchResultFinder>();

    [Fact(DisplayName =
        "When the search-result finder resolves by exact episode title, " +
        "it returns the SearchResult whose title and duration match the stored episode.")]
    public async Task find_by_exact_title_delegates_duration_validation_and_maps_back()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var episodeLength = TimeSpan.FromHours(1);
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = sharedTitle;
                e.Length = episodeLength;
            })
            .Create();
        var searchResults = new List<SearchResult>
        {
            CreateSearchResult(videoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(videoId, episodeLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            searchResults,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.SearchResult!.Id.VideoId.Should().Be(videoId);
    }

    [Fact(DisplayName =
        "When the search-result finder matches on exact episode title but video duration is unacceptable, " +
        "no SearchResult is returned.")]
    public async Task exact_title_match_rejects_unacceptable_video_duration()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var episodeLength = TimeSpan.FromHours(1);
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = sharedTitle;
                e.Length = episodeLength;
            })
            .Create();
        var searchResults = new List<SearchResult>
        {
            CreateSearchResult(videoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(videoId, TimeSpan.FromMinutes(10));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            searchResults,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When multiple search results share an exact substring title match, " +
        "the search-result finder falls through to duration-only matching.")]
    public async Task multiple_exact_title_candidates_fall_through_to_duration_only_match()
    {
        // Arrange
        var sharedTitle = _fixture.CreateShortTitle();
        var episodeLength = TimeSpan.FromHours(1);
        var firstVideoId = _fixture.CreateYouTubeId();
        var secondVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = sharedTitle;
                e.Length = episodeLength;
            })
            .Create();
        var searchResults = new List<SearchResult>
        {
            CreateSearchResult(firstVideoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1)),
            CreateSearchResult(secondVideoId, $"{sharedTitle} extended cut", DomainTestFixture.UtcDaysAgo(2))
        };
        ConfigureVideoDurations(
            (firstVideoId, episodeLength),
            (secondVideoId, episodeLength));

        // Act â€” falls through exact-title ambiguity; duration-only picks closest (both equal here).
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            searchResults,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.SearchResult!.Id.VideoId.Should().BeOneOf(firstVideoId, secondVideoId);
    }

    [Theory(DisplayName =
        "When duration-only matching fails but publish delay aligns via IsCatalogueMatch, " +
        "each fuzzy title variant on a short title resolves the delayed YouTube video.")]
    [MemberData(nameof(AllFuzzyVariantStrategies))]
    public async Task find_by_publish_delay_delegates_for_each_fuzzy_variant_on_short_title(
        FuzzyTitleVariantStrategy strategy)
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(episodeTitle, strategy);
        AssertFuzzyScoreAboveThreshold(episodeTitle, catalogueTitle, strategy);

        var (episode, matchingVideoId, searchResults, catalogueVideoLength) =
            BuildPublishDelayScenario(episodeTitle, catalogueTitle, durationOffsetFromEpisode: TimeSpan.FromMinutes(3));
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            searchResults,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.SearchResult!.Id.VideoId.Should().Be(matchingVideoId);
    }

    [Fact(DisplayName =
        "When publish delay catalogue matching succeeds but video duration exceeds the 5-minute publication tolerance, " +
        "no SearchResult is returned.")]
    public async Task publish_delay_match_rejects_duration_outside_publication_tolerance()
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            episodeTitle, FuzzyTitleVariantStrategy.ReplaceWord);
        AssertFuzzyScoreAboveThreshold(
            episodeTitle, catalogueTitle, FuzzyTitleVariantStrategy.ReplaceWord);

        var (episode, matchingVideoId, searchResults, _) =
            BuildPublishDelayScenario(episodeTitle, catalogueTitle, durationOffsetFromEpisode: TimeSpan.FromMinutes(6));
        ConfigureVideoDuration(matchingVideoId, episode.Length - TimeSpan.FromMinutes(6));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            searchResults,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Theory(DisplayName =
        "When earlier matchers fail and no publish delay is configured, " +
        "each fuzzy title variant on a short title resolves via local text closeness.")]
    [MemberData(nameof(AllFuzzyVariantStrategies))]
    public async Task find_by_text_closeness_for_each_fuzzy_variant_on_short_title(
        FuzzyTitleVariantStrategy strategy)
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(episodeTitle, strategy);
        AssertFuzzyScoreAboveThreshold(episodeTitle, catalogueTitle, strategy);

        var episodeLength = TimeSpan.FromHours(1);
        var catalogueVideoLength = episodeLength - TimeSpan.FromMinutes(3);
        var matchingVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = episodeTitle;
                e.Length = episodeLength;
            })
            .Create();
        var searchResults = new List<SearchResult>
        {
            CreateSearchResult(matchingVideoId, catalogueTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            searchResults,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.SearchResult!.Id.VideoId.Should().Be(matchingVideoId);
    }

    private (
        RedditPodcastPoster.Models.Episode Episode,
        string MatchingVideoId,
        List<SearchResult> SearchResults,
        TimeSpan CatalogueVideoLength) BuildPublishDelayScenario(
        string episodeTitle,
        string catalogueTitle,
        TimeSpan durationOffsetFromEpisode)
    {
        var episodeLength = TimeSpan.FromHours(1);
        var catalogueVideoLength = episodeLength - durationOffsetFromEpisode;
        var release = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var appleId = _fixture.CreateAppleId();
        var spotifyId = _fixture.CreateSpotifyId();
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId);
        var matchingVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = episodeTitle;
                e.Length = episodeLength;
                e.Release = release;
                e.AppleId = appleId;
                e.SpotifyId = spotifyId;
                e.Urls = new ServiceUrls { Apple = appleUrl, Spotify = spotifyUrl };
            })
            .Create();
        var publishedAt = release.Add(TimeSpan.FromHours(-6));
        var searchResults = new List<SearchResult>
        {
            CreateSearchResult(matchingVideoId, catalogueTitle, publishedAt)
        };

        return (episode, matchingVideoId, searchResults, catalogueVideoLength);
    }

    private static void AssertFuzzyScoreAboveThreshold(
        string episodeTitle,
        string catalogueTitle,
        FuzzyTitleVariantStrategy strategy)
    {
        Fuzz.WeightedRatio(episodeTitle, catalogueTitle)
            .Should().BeGreaterThanOrEqualTo(
                MinFuzzyTitleScore,
                "variant strategy {0} must produce a title within FuzzySharp threshold â€” " +
                "episode=\"{1}\" catalogue=\"{2}\"",
                strategy, episodeTitle, catalogueTitle);
    }

    private void ConfigureVideoDuration(string videoId, TimeSpan duration)
    {
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((
                IYouTubeServiceWrapper _,
                IEnumerable<string> videoIds,
                IndexingContext _,
                bool _,
                bool _) =>
                videoIds.Select(id =>
                        CreateCompletedVideo(
                            id,
                            id == videoId ? duration : TimeSpan.FromMinutes(20)))
                    .ToList());
    }

    private void ConfigureVideoDurations(params (string VideoId, TimeSpan Duration)[] durations)
    {
        var durationByVideoId = durations.ToDictionary(x => x.VideoId, x => x.Duration);
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((
                IYouTubeServiceWrapper _,
                IEnumerable<string> videoIds,
                IndexingContext _,
                bool _,
                bool _) =>
                videoIds.Select(id =>
                        CreateCompletedVideo(
                            id,
                            durationByVideoId.TryGetValue(id, out var duration)
                                ? duration
                                : TimeSpan.FromMinutes(20)))
                    .ToList());
    }

    private static SearchResult CreateSearchResult(
        string videoId,
        string title,
        DateTimeOffset publishedAt) =>
        new()
        {
            Id = new ResourceId { VideoId = videoId },
            Snippet = new SearchResultSnippet
            {
                Title = title,
                PublishedAtDateTimeOffset = publishedAt
            }
        };

    private static Google.Apis.YouTube.v3.Data.Video CreateCompletedVideo(string videoId, TimeSpan duration) =>
        new()
        {
            Id = videoId,
            ContentDetails = new VideoContentDetails
            {
                Duration = XmlConvert.ToString(duration)
            },
            Snippet = new VideoSnippet { LiveBroadcastContent = "none" }
        };
}
