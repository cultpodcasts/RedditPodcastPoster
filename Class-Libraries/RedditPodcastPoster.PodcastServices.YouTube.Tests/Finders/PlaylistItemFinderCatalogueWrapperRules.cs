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

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Finders;

/// <summary>
/// Playlist finder characterization: exact-title matching, publish-delay delegation to
/// <see cref="RedditPodcastPoster.Episodes.Matching.IEpisodePlatformMatcher.IsCatalogueMatch"/>,
/// local fuzzy-title closeness (FuzzySharp ≥ 70), and wrapper-specific duration gates.
/// Fuzzy/heuristic paths use explicit <see cref="FuzzyTitleVariantStrategy"/> matrix rows per unit-tests.mdc §7.
/// </summary>
public class PlaylistItemFinderCatalogueWrapperRules
{
    private const int MinFuzzyTitleScore = 70;

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PlaylistItemFinderCatalogueWrapperRules()
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

    private IPlaylistItemFinder Sut => _mocker.CreateInstance<PlaylistItemFinder>();

    // --------------------------------------------------------------------------------------------
    // Deterministic exact-title path (not a scored heuristic).
    // --------------------------------------------------------------------------------------------

    [Fact(DisplayName =
        "When the playlist finder resolves by exact episode title, " +
        "it returns the PlaylistItem whose title and duration match the stored episode.")]
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
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(videoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(videoId, episodeLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(videoId);
    }

    [Fact(DisplayName =
        "When the playlist finder matches on exact episode title but video duration is unacceptable, " +
        "no PlaylistItem is returned.")]
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
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(videoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(videoId, TimeSpan.FromMinutes(10));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    // --------------------------------------------------------------------------------------------
    // Publish-delay path: domain IsCatalogueMatch + local 5-minute duration gate.
    // --------------------------------------------------------------------------------------------

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

        var (episode, matchingVideoId, playlistItems, catalogueVideoLength) =
            BuildPublishDelayScenario(episodeTitle, catalogueTitle, durationOffsetFromEpisode: TimeSpan.FromMinutes(3));
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(matchingVideoId);
    }

    [Fact(DisplayName =
        "When publish delay catalogue matching succeeds but video duration exceeds the 5-minute publication tolerance, " +
        "no PlaylistItem is returned.")]
    public async Task publish_delay_match_rejects_duration_outside_publication_tolerance()
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            episodeTitle, FuzzyTitleVariantStrategy.ReplaceWord);
        AssertFuzzyScoreAboveThreshold(
            episodeTitle, catalogueTitle, FuzzyTitleVariantStrategy.ReplaceWord);

        var (episode, matchingVideoId, playlistItems, _) =
            BuildPublishDelayScenario(episodeTitle, catalogueTitle, durationOffsetFromEpisode: TimeSpan.FromMinutes(6));
        ConfigureVideoDuration(matchingVideoId, episode.Length - TimeSpan.FromMinutes(6));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    // --------------------------------------------------------------------------------------------
    // Local fuzzy-title closeness fallback (FuzzySharp ≥ 70) when earlier matchers fail.
    // Duration offset skips the 2-minute duration-only path but still passes long-form validation.
    // --------------------------------------------------------------------------------------------

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
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, catalogueTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(matchingVideoId);
    }

    [Theory(DisplayName =
        "When earlier matchers fail and no publish delay is configured, " +
        "each fuzzy title variant on a long title resolves via local text closeness.")]
    [MemberData(nameof(AllFuzzyVariantStrategies))]
    public async Task find_by_text_closeness_for_each_fuzzy_variant_on_long_title(
        FuzzyTitleVariantStrategy strategy)
    {
        // Arrange
        var episodeTitle = _fixture.CreateLongTitle();
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
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, catalogueTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(matchingVideoId);
    }

    [Fact(DisplayName =
        "When playlist item titles share no meaningful word overlap and fall below the fuzzy threshold, " +
        "no PlaylistItem is returned even when duration would otherwise align.")]
    public async Task unrelated_titles_below_fuzzy_threshold_return_no_match()
    {
        // Arrange
        const string episodeTitle = "History Of Ancient Roman Politics";
        const string catalogueTitle = "Modern Quantum Physics Research Highlights";
        Fuzz.WeightedRatio(episodeTitle, catalogueTitle)
            .Should().BeLessThan(MinFuzzyTitleScore);

        var episodeLength = TimeSpan.FromHours(1);
        var matchingVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = episodeTitle;
                e.Length = episodeLength;
            })
            .Create();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, catalogueTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        // Offset > 2-minute duration-only tolerance so only fuzzy closeness is exercised — and fails.
        ConfigureVideoDuration(matchingVideoId, episodeLength - TimeSpan.FromMinutes(3));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    private (
        RedditPodcastPoster.Models.Episode Episode,
        string MatchingVideoId,
        List<PlaylistItem> PlaylistItems,
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
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, catalogueTitle, publishedAt)
        };

        return (episode, matchingVideoId, playlistItems, catalogueVideoLength);
    }

    private static void AssertFuzzyScoreAboveThreshold(
        string episodeTitle,
        string catalogueTitle,
        FuzzyTitleVariantStrategy strategy)
    {
        Fuzz.WeightedRatio(episodeTitle, catalogueTitle)
            .Should().BeGreaterThanOrEqualTo(
                MinFuzzyTitleScore,
                "variant strategy {0} must produce a title within FuzzySharp threshold — " +
                "episode=\"{1}\" catalogue=\"{2}\"",
                strategy, episodeTitle, catalogueTitle);
    }

    private void ConfigureVideoDuration(string videoId, TimeSpan duration)
    {
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Contains(videoId)),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((
                IYouTubeServiceWrapper _,
                IEnumerable<string> videoIds,
                IndexingContext _,
                bool _,
                bool _) =>
                videoIds.Select(id => CreateCompletedVideo(id, id == videoId ? duration : TimeSpan.FromMinutes(20)))
                    .ToList());
    }

    private static PlaylistItem CreatePlaylistItem(
        string videoId,
        string title,
        DateTimeOffset publishedAt) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                Title = title,
                ResourceId = new ResourceId { VideoId = videoId },
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
