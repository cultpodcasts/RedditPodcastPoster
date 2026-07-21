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
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Finders;

/// <summary>
/// Playlist finder characterization: exact-title matching, publish-delay delegation to
/// <see cref="RedditPodcastPoster.Episodes.Matching.IEpisodePlatformMatcher.IsCatalogueMatch"/>,
/// local fuzzy-title closeness (FuzzySharp â‰¥ 70), and wrapper-specific duration gates.
/// Fuzzy/heuristic paths use explicit <see cref="FuzzyTitleVariantStrategy"/> matrix rows per unit-tests.mdc Â§7.
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
    // Local fuzzy-title closeness fallback (FuzzySharp â‰¥ 70) when earlier matchers fail.
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
        // Offset > 2-minute duration-only tolerance so only fuzzy closeness is exercised â€” and fails.
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

    // --------------------------------------------------------------------------------------------
    // Episode-number and duration-only local heuristics (after exact-title path fails).
    // --------------------------------------------------------------------------------------------

    [Theory(DisplayName =
        "When exact title matching fails, each fuzzy title variant on a short title may still resolve " +
        "via shared episode number and acceptable video duration.")]
    [MemberData(nameof(AllFuzzyVariantStrategies))]
    public async Task find_by_episode_number_for_each_fuzzy_variant_on_short_title(
        FuzzyTitleVariantStrategy strategy)
    {
        // Arrange
        const int episodeNumber = 42;
        var episodeTitle = $"Series discussion part {episodeNumber} opening themes";
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            $"Series recap part {episodeNumber} closing thoughts",
            strategy);

        var episodeLength = TimeSpan.FromHours(1);
        var catalogueVideoLength = episodeLength - TimeSpan.FromSeconds(30);
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
        "When episode-number matching succeeds but video duration is unacceptable, " +
        "no PlaylistItem is returned.")]
    public async Task episode_number_match_rejects_unacceptable_video_duration()
    {
        // Arrange
        const int episodeNumber = 17;
        var sharedPrefix = "Weekly show";
        var episodeTitle = $"{sharedPrefix} episode {episodeNumber} part one";
        var catalogueTitle = $"{sharedPrefix} episode {episodeNumber} part two";
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
        ConfigureVideoDuration(matchingVideoId, TimeSpan.FromMinutes(10));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Theory(DisplayName =
        "When exact title, episode number, and publish-delay paths fail, " +
        "duration-only matching may still resolve a video within the two-minute tolerance.")]
    [InlineData(0)]
    [InlineData(90)]
    public async Task find_by_duration_only_within_two_minute_tolerance(long offsetSeconds)
    {
        // Arrange
        var episodeLength = TimeSpan.FromHours(1);
        var catalogueVideoLength = episodeLength - TimeSpan.FromSeconds(offsetSeconds);
        var matchingVideoId = _fixture.CreateYouTubeId();
        var distractorVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = "Quantum computing fundamentals overview";
                e.Length = episodeLength;
            })
            .Create();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, "Ancient pottery restoration techniques", DomainTestFixture.UtcDaysAgo(1)),
            CreatePlaylistItem(distractorVideoId, "Modern urban gardening tips", DomainTestFixture.UtcDaysAgo(2))
        };
        ConfigureVideoDurations(
            (matchingVideoId, catalogueVideoLength),
            (distractorVideoId, TimeSpan.FromMinutes(20)));

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
        "When multiple playlist items share an exact substring title match, " +
        "the playlist finder does not resolve via exact title alone.")]
    public async Task multiple_exact_title_candidates_do_not_resolve_via_exact_title_path()
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
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(firstVideoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1)),
            CreatePlaylistItem(secondVideoId, $"{sharedTitle} extended cut", DomainTestFixture.UtcDaysAgo(2))
        };
        ConfigureVideoDurations(
            (firstVideoId, episodeLength),
            (secondVideoId, episodeLength));

        // Act â€” falls through exact-title ambiguity; duration-only picks closest (both equal here).
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().BeOneOf(firstVideoId, secondVideoId);
    }

    [Fact(DisplayName =
        "When live or upcoming playlist videos are filtered out, " +
        "matching proceeds against completed public videos only.")]
    public async Task live_and_upcoming_videos_are_excluded_before_matching()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var episodeLength = TimeSpan.FromHours(1);
        var liveVideoId = _fixture.CreateYouTubeId();
        var completedVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = sharedTitle;
                e.Length = episodeLength;
            })
            .Create();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(liveVideoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1)),
            CreatePlaylistItem(completedVideoId, sharedTitle, DomainTestFixture.UtcDaysAgo(2))
        };
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
                bool withSnippets) =>
            {
                return videoIds.Select(id => id == liveVideoId
                    ? CreateVideo(id, episodeLength, liveBroadcastContent: "live")
                    : CreateCompletedVideo(id, episodeLength)).ToList();
            });

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(completedVideoId);
    }

    [Fact(DisplayName =
        "When duration-only matching finds a closest video outside the two-minute tolerance, " +
        "no PlaylistItem is returned.")]
    public async Task duration_only_match_rejects_offset_beyond_two_minute_tolerance()
    {
        // Arrange
        var episodeLength = TimeSpan.FromHours(1);
        var matchingVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = "Independent documentary filmmaking";
                e.Length = episodeLength;
            })
            .Create();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, "Unrelated sports highlights", DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDurations((matchingVideoId, episodeLength - TimeSpan.FromMinutes(3)));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When live filtering leaves no completed public videos, the playlist finder returns no match.")]
    public async Task empty_playlist_after_live_filter_returns_no_match()
    {
        // Arrange
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = _fixture.CreateTitle();
                e.Length = TimeSpan.FromHours(1);
            })
            .Create();
        var liveVideoId = _fixture.CreateYouTubeId();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(liveVideoId, episode.Title, DomainTestFixture.UtcDaysAgo(1))
        };
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
                videoIds.Select(id => CreateVideo(id, episode.Length, liveBroadcastContent: "live")).ToList());

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When publish-delay catalogue matching succeeds and video duration is within the 5-minute " +
        "publication tolerance, the matching PlaylistItem is returned.")]
    public async Task publish_delay_match_accepts_duration_within_publication_tolerance()
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            episodeTitle, FuzzyTitleVariantStrategy.ReplaceWord);
        AssertFuzzyScoreAboveThreshold(
            episodeTitle, catalogueTitle, FuzzyTitleVariantStrategy.ReplaceWord);

        var (episode, matchingVideoId, playlistItems, catalogueVideoLength) =
            BuildPublishDelayScenario(
                episodeTitle,
                catalogueTitle,
                durationOffsetFromEpisode: TimeSpan.FromMinutes(2));
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
        "When multiple playlist titles share the same episode number and other matchers also fail, " +
        "no PlaylistItem is returned.")]
    public async Task ambiguous_episode_number_match_does_not_resolve()
    {
        // Arrange
        var episodeLength = TimeSpan.FromHours(1);
        var firstVideoId = _fixture.CreateYouTubeId();
        var secondVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = "Episode 42 â€” Monday briefing";
                e.Length = episodeLength;
            })
            .Create();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(firstVideoId, "Episode 42 â€” studio cut", DomainTestFixture.UtcDaysAgo(1)),
            CreatePlaylistItem(secondVideoId, "Episode 42 â€” extended cut", DomainTestFixture.UtcDaysAgo(2))
        };
        // Durations beyond the two-minute duration-only path so only ambiguous number matching is in play.
        ConfigureVideoDurations(
            (firstVideoId, episodeLength - TimeSpan.FromMinutes(8)),
            (secondVideoId, episodeLength + TimeSpan.FromMinutes(8)));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the stored episode lacks accurate release time, publish-delay matching is skipped " +
        "even if a delayed YouTube publish would otherwise align.")]
    public async Task publish_delay_skipped_without_accurate_release_time()
    {
        // Arrange
        var episodeTitle = "Economics of Cheese";
        var catalogueTitle = "Quantum Gardening Weekly";
        var episodeLength = TimeSpan.FromHours(1);
        var release = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var matchingVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = episodeTitle;
                e.Length = episodeLength;
                e.Release = release;
                e.AppleId = null;
                e.Urls = new ServiceUrls();
            })
            .Create();
        var publishedAt = release.Add(TimeSpan.FromHours(-6));
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, catalogueTitle, publishedAt)
        };
        ConfigureVideoDuration(matchingVideoId, episodeLength - TimeSpan.FromMinutes(3));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the closest playlist publish time is more than one day from the delay-adjusted expectation, " +
        "publish-delay matching does not resolve a video.")]
    public async Task publish_delay_rejects_when_closest_publish_exceeds_one_day()
    {
        // Arrange
        var episodeTitle = "Economics of Cheese";
        var catalogueTitle = "Quantum Gardening Weekly";
        var (episode, matchingVideoId, playlistItems, catalogueVideoLength) =
            BuildPublishDelayScenario(
                episodeTitle,
                catalogueTitle,
                durationOffsetFromEpisode: TimeSpan.FromMinutes(8));
        var misalignedPublish = episode.Release
            .Add(TimeSpan.FromHours(-6))
            .AddDays(2);
        playlistItems[0].Snippet.PublishedAtDateTimeOffset = misalignedPublish;
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When publish time aligns but domain catalogue matching rejects the candidate, " +
        "no PlaylistItem is returned.")]
    public async Task publish_delay_rejects_when_domain_catalogue_match_fails()
    {
        // Arrange
        var episodeTitle = "Economics of Cheese";
        var catalogueTitle = "Quantum Gardening Weekly";
        var (episode, matchingVideoId, playlistItems, catalogueVideoLength) =
            BuildPublishDelayScenario(
                episodeTitle,
                catalogueTitle,
                durationOffsetFromEpisode: TimeSpan.FromMinutes(8));
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);
        var matcher = new Mock<RedditPodcastPoster.Episodes.Matching.IEpisodePlatformMatcher>();
        matcher
            .Setup(x => x.IsCatalogueMatch(
                It.IsAny<EpisodeModel>(),
                It.IsAny<EpisodeModel>(),
                It.IsAny<Podcast>(),
                null))
            .Returns(false);
        _mocker.Use(matcher.Object);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When publish-delay catalogue matching succeeds but the YouTube video is shorter than five minutes, " +
        "no PlaylistItem is returned.")]
    public async Task publish_delay_rejects_when_video_shorter_than_minimum_publication_duration()
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            episodeTitle,
            FuzzyTitleVariantStrategy.ReplaceWord);
        AssertFuzzyScoreAboveThreshold(
            episodeTitle, catalogueTitle, FuzzyTitleVariantStrategy.ReplaceWord);
        var (episode, matchingVideoId, playlistItems, _) =
            BuildPublishDelayScenario(episodeTitle, catalogueTitle, durationOffsetFromEpisode: TimeSpan.FromMinutes(2));
        ConfigureVideoDuration(matchingVideoId, TimeSpan.FromMinutes(4));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: TimeSpan.FromHours(-6),
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a single playlist title contains the stored episode title as a substring, " +
        "exact-title matching resolves that candidate after duration validation.")]
    public async Task exact_title_substring_match_resolves_single_candidate()
    {
        // Arrange
        const string episodeTitle = "beta";
        const string catalogueTitle = "extended beta episode special";
        var episodeLength = TimeSpan.FromHours(1);
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = episodeTitle;
                e.Length = episodeLength;
            })
            .Create();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(videoId, catalogueTitle, DomainTestFixture.UtcDaysAgo(1))
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
        "When fuzzy title matching finds a candidate but per-video duration validation fails, " +
        "no PlaylistItem is returned.")]
    public async Task fuzzy_match_rejects_unacceptable_video_duration()
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            episodeTitle,
            FuzzyTitleVariantStrategy.ReplaceWord);
        AssertFuzzyScoreAboveThreshold(
            episodeTitle, catalogueTitle, FuzzyTitleVariantStrategy.ReplaceWord);
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
        ConfigureVideoDuration(matchingVideoId, TimeSpan.FromMinutes(10));

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When an exact-title candidate is found but YouTube returns no video details for that id, " +
        "no PlaylistItem is returned.")]
    public async Task exact_title_match_returns_null_when_video_details_missing()
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
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Single() == videoId),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When live/upcoming filtering cannot load video details, the finder keeps the original playlist " +
        "and may still match completed-looking items.")]
    public async Task live_filter_skipped_when_video_details_unavailable()
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
                bool withSnippets) =>
            {
                if (withSnippets)
                {
                    return null;
                }

                return videoIds
                    .Select(id => CreateCompletedVideo(id, episodeLength))
                    .ToList();
            });

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

    private (
        EpisodeModel Episode,
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
                "variant strategy {0} must produce a title within FuzzySharp threshold â€” " +
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
        CreateVideo(videoId, duration, liveBroadcastContent: "none");

    private static Google.Apis.YouTube.v3.Data.Video CreateVideo(
        string videoId,
        TimeSpan duration,
        string liveBroadcastContent) =>
        new()
        {
            Id = videoId,
            ContentDetails = new VideoContentDetails
            {
                Duration = XmlConvert.ToString(duration)
            },
            Snippet = new VideoSnippet { LiveBroadcastContent = liveBroadcastContent }
        };
}
