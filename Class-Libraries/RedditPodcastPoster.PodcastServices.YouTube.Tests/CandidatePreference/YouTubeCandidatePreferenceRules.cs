using System.Xml;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.CandidatePreference;

/// <summary>
/// Business rules for choosing between YouTube candidates of the same episode
/// (enrich / index-enrich / submit lookups all go through <see cref="IPlaylistItemFinder"/> and
/// <see cref="IYouTubeSearchResultFinder"/>). Real cases:
/// IndoctriNation (channel UC67JLTD22dSnVadqXKcYPcA) uploads each episode twice: a real video
/// ("The Oracle's Daughter with Sarah Green", 4AcC36Aw2nY, auto-thumbnail diff 32.54) and a still image
/// ("IndoctriNation - The Oracle's Daughter with Sarah Green", S03n89L5YLA, diff 3.10).
/// The Afro Atheist (UC-Vn8JbQ4sbMKYzz7HFMtFg) uploads duplicates with a trailing 📱 in snippet.title
/// ("EL-ROI CANNOT SAVE ANYONE 📱", HIuO5jaDWFs, vs "EL-ROI CANNOT SAVE ANYONE", ExbrJpfl4Cg).
/// </summary>
public class YouTubeCandidatePreferenceRules
{
    private const string StaticUploadId = "S03n89L5YLA";
    private const string VideoUploadId = "4AcC36Aw2nY";
    private const string PhoneUploadId = "HIuO5jaDWFs";
    private const string PlainUploadId = "ExbrJpfl4Cg";
    private static readonly DateTimeOffset Release = new(2026, 9, 20, 17, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan EpisodeLength = TimeSpan.FromMinutes(62);

    private readonly AutoMocker _mocker = new();
    private readonly Dictionary<string, TimeSpan> _durations = new();
    private readonly Mock<IYouTubeThumbnailMotionDetector> _motion = new();
    private YouTubeCandidatePreferenceOptions _options = new();

    public YouTubeCandidatePreferenceRules()
    {
        _mocker.Use(EpisodeDomainTestServices.CreatePlatformMatcher());
        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync((IYouTubeServiceWrapper _, IEnumerable<string> ids, IndexingContext _, bool _, bool _, bool _) =>
                ids.Where(_durations.ContainsKey).Select(id => Video(id, _durations[id])).ToList());
        _motion.Setup(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThumbnailMotion.Moving);
    }

    private IPlaylistItemFinder PlaylistSut()
    {
        var preference = new YouTubeCandidatePreference(
            _motion.Object, Options.Create(_options), NullLogger<YouTubeCandidatePreference>.Instance);
        var applier = new YouTubeCandidatePreferenceApplier(
            _mocker.Get<IYouTubeServiceWrapper>(),
            _mocker.Get<ITolerantYouTubeVideoService>(),
            preference,
            Options.Create(_options),
            NullLogger<YouTubeCandidatePreferenceApplier>.Instance);
        return new PreferringPlaylistItemFinder(_mocker.CreateInstance<PlaylistItemFinder>(), applier);
    }

    private IYouTubeSearchResultFinder SearchSut()
    {
        var preference = new YouTubeCandidatePreference(
            _motion.Object, Options.Create(_options), NullLogger<YouTubeCandidatePreference>.Instance);
        var applier = new YouTubeCandidatePreferenceApplier(
            _mocker.Get<IYouTubeServiceWrapper>(),
            _mocker.Get<ITolerantYouTubeVideoService>(),
            preference,
            Options.Create(_options),
            NullLogger<YouTubeCandidatePreferenceApplier>.Instance);
        return new PreferringYouTubeSearchResultFinder(_mocker.CreateInstance<YouTubeSearchResultFinder>(), applier);
    }

    [Fact(DisplayName =
        "When a channel has both a static-artwork upload and a real video upload of the same episode, " +
        "the real video is chosen even though the static upload is the closer duration match.")]
    public async Task static_upload_loses_to_video_upload_of_same_episode()
    {
        // Arrange
        const string title = "The Oracle's Daughter with Sarah Green";
        var episode = Episode(title);
        _durations[StaticUploadId] = EpisodeLength;
        _durations[VideoUploadId] = EpisodeLength + TimeSpan.FromSeconds(40);
        _motion.Setup(x => x.DetectAsync(StaticUploadId, It.IsAny<CancellationToken>())).ReturnsAsync(ThumbnailMotion.Static);
        var items = new List<PlaylistItem> { Item(StaticUploadId, StaticTitle(title)), Item(VideoUploadId, title) };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(episode, items, null, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(VideoUploadId);
        result.Video!.Id.Should().Be(VideoUploadId);
    }

    [Fact(DisplayName =
        "When two uploads of the same episode differ only by a 📱 in the title, the upload without 📱 is chosen.")]
    public async Task phone_emoji_title_loses_to_plain_title()
    {
        // Arrange
        const string plainTitle = "EL-ROI CANNOT SAVE ANYONE";
        var episode = Episode(plainTitle);
        _durations[PhoneUploadId] = EpisodeLength;
        _durations[PlainUploadId] = EpisodeLength + TimeSpan.FromSeconds(30);
        var items = new List<PlaylistItem>
        {
            Item(PhoneUploadId, plainTitle + " \U0001F4F1"),
            Item(PlainUploadId, plainTitle)
        };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(episode, items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(PlainUploadId);
    }

    [Fact(DisplayName =
        "When search results hold a 📱 and a plain upload of the same episode, search lookups also choose the plain upload.")]
    public async Task search_lookup_prefers_plain_title()
    {
        // Arrange
        const string plainTitle = "EL-ROI CANNOT SAVE ANYONE";
        var episode = Episode(plainTitle);
        _durations[PhoneUploadId] = EpisodeLength;
        _durations[PlainUploadId] = EpisodeLength + TimeSpan.FromSeconds(30);
        var results = new List<SearchResult>
        {
            Search(PhoneUploadId, plainTitle + " \U0001F4F1"),
            Search(PlainUploadId, plainTitle)
        };

        // Act
        var result = await SearchSut().FindMatchingYouTubeVideo(episode, results, null, new IndexingContext());

        // Assert
        result!.SearchResult!.Id.VideoId.Should().Be(PlainUploadId);
    }

    [Fact(DisplayName = "When there is only one candidate, no thumbnails are fetched and the finder's match stands.")]
    public async Task single_candidate_fetches_no_thumbnails()
    {
        // Arrange
        const string title = "The Oracle's Daughter with Sarah Green";
        _durations[StaticUploadId] = EpisodeLength;

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(
            Episode(title), [Item(StaticUploadId, StaticTitle(title))], null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When other candidates are different episodes (dissimilar titles), no thumbnails are fetched.")]
    public async Task unrelated_candidates_fetch_no_thumbnails()
    {
        // Arrange
        const string title = "The Oracle's Daughter with Sarah Green";
        _durations[StaticUploadId] = EpisodeLength;
        _durations[VideoUploadId] = TimeSpan.FromMinutes(48);
        var items = new List<PlaylistItem>
        {
            Item(StaticUploadId, StaticTitle(title)),
            Item(VideoUploadId, "Jules Evans on Psychedelics and Cultic Influence")
        };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(title), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a same-titled contender fails duration validation, it cannot replace the match and no thumbnails are fetched.")]
    public async Task contender_failing_duration_cannot_replace_match()
    {
        // Arrange
        const string title = "The Oracle's Daughter with Sarah Green";
        _durations[StaticUploadId] = EpisodeLength;
        _durations[VideoUploadId] = TimeSpan.FromMinutes(1);
        _motion.Setup(x => x.DetectAsync(StaticUploadId, It.IsAny<CancellationToken>())).ReturnsAsync(ThumbnailMotion.Static);
        var items = new List<PlaylistItem> { Item(StaticUploadId, StaticTitle(title)), Item(VideoUploadId, title) };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(title), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When motion cannot be determined (missing or placeholder thumbnails), no penalty applies and the finder's match stands.")]
    public async Task unknown_motion_is_fail_open()
    {
        // Arrange
        const string title = "The Oracle's Daughter with Sarah Green";
        _durations[StaticUploadId] = EpisodeLength;
        _durations[VideoUploadId] = EpisodeLength + TimeSpan.FromSeconds(40);
        _motion.Setup(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ThumbnailMotion.Unknown);
        var items = new List<PlaylistItem> { Item(StaticUploadId, StaticTitle(title)), Item(VideoUploadId, title) };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(title), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
    }

    [Fact(DisplayName = "When both contenders carry the same penalties, the finder's original match is kept.")]
    public async Task tie_keeps_original_match()
    {
        // Arrange
        const string title = "The Oracle's Daughter with Sarah Green";
        _durations[StaticUploadId] = EpisodeLength;
        _durations[VideoUploadId] = EpisodeLength + TimeSpan.FromSeconds(40);
        _motion.Setup(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ThumbnailMotion.Static);
        var items = new List<PlaylistItem> { Item(StaticUploadId, StaticTitle(title)), Item(VideoUploadId, title) };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(title), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
    }

    [Fact(DisplayName = "When candidate preference is disabled, the finder's match is returned unchanged.")]
    public async Task disabled_returns_finder_match()
    {
        // Arrange
        _options = new YouTubeCandidatePreferenceOptions { Enabled = false };
        const string plainTitle = "EL-ROI CANNOT SAVE ANYONE";
        _durations[PhoneUploadId] = EpisodeLength;
        _durations[PlainUploadId] = EpisodeLength + TimeSpan.FromSeconds(30);
        var items = new List<PlaylistItem>
        {
            Item(PhoneUploadId, plainTitle + " \U0001F4F1"),
            Item(PlainUploadId, plainTitle)
        };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(plainTitle), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(PhoneUploadId);
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>IndoctriNation publishes its still-image upload as "IndoctriNation - &lt;episode title&gt;".</summary>
    private static string StaticTitle(string title) => "IndoctriNation - " + title;

    private static EpisodeModel Episode(string title) =>
        new() { Id = Guid.NewGuid(), Title = title, Length = EpisodeLength, ReleaseUtc = Release.UtcDateTime };

    private static PlaylistItem Item(string id, string title) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                Title = title,
                ResourceId = new ResourceId { VideoId = id },
                PublishedAtDateTimeOffset = Release
            }
        };

    private static SearchResult Search(string id, string title) =>
        new()
        {
            Id = new ResourceId { VideoId = id, Kind = "youtube#video" },
            Snippet = new SearchResultSnippet { Title = title, PublishedAtDateTimeOffset = Release }
        };

    private static Google.Apis.YouTube.v3.Data.Video Video(string id, TimeSpan length) =>
        new()
        {
            Id = id,
            ContentDetails = new VideoContentDetails { Duration = XmlConvert.ToString(length) },
            Snippet = new VideoSnippet { LiveBroadcastContent = "none" }
        };
}