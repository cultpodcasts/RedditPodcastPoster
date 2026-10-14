using System.Xml;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport; // pragma: allowlist secret
using RedditPodcastPoster.Episodes.Matching; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Models; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Clients; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Services; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Video; // pragma: allowlist secret
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode; // pragma: allowlist secret

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.CandidatePreference; // pragma: allowlist secret

/// <summary>
/// Business rules for choosing between YouTube candidates of the same episode
/// (enrich / index-enrich / submit lookups all go through <see cref="IPlaylistItemFinder"/> and
/// <see cref="IYouTubeSearchResultFinder"/>). Real cases:
/// IndoctriNation (channel UC67JLTD22dSnVadqXKcYPcA) uploads each episode twice: a real video
/// ("The Oracle's Daughter with Sarah Green", 4AcC36Aw2nY, auto-thumbnail diff 32.54) and a still image
/// ("IndoctriNation - The Oracle's Daughter with Sarah Green", S03n89L5YLA, diff 3.10).
/// SkiaSharp decoding reproduces those two diffs, and the 📱 upload HIuO5jaDWFs at 6.93.
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
                ids.Where(id => id != null && _durations.ContainsKey(id)).Select(id => Video(id, _durations[id])).ToList());
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
        "When other candidates are different episodes (dissimilar titles), no thumbnails are fetched.")] // pragma: allowlist secret
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

    [Fact(DisplayName =
        "When the channel trailer title is below the similarity gate, preference does not fetch that video on its own.")]
    public async Task channel_trailer_below_similarity_gate_is_not_fetched()
    {
        // Arrange: real channel trailer "IndoctriNation Podcast" (DJg4gR1j4n0). Its tokens are not a subset of the pick.
        const string title = "The Oracle's Daughter with Sarah Green";
        const string channelTrailerId = "DJg4gR1j4n0";
        _durations[StaticUploadId] = EpisodeLength;
        _durations[channelTrailerId] = TimeSpan.FromMinutes(2);
        var items = new List<PlaylistItem>
        {
            Item(StaticUploadId, StaticTitle(title)),
            Item(channelTrailerId, "IndoctriNation Podcast")
        };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(title), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
        VerifyPreferenceFetched(channelTrailerId, Times.Never());
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a contender title is a true subset of the pick and its duration fails the duration guard, " +
        "the video is fetched and cannot replace the pick, and no thumbnails are fetched.")]
    public async Task subset_title_failing_duration_cannot_replace_pick()
    {
        // Arrange: "IndoctriNation" is a token subset of "IndoctriNation - <episode title>". Two minutes fails the duration guard.
        const string title = "The Oracle's Daughter with Sarah Green";
        const string subsetUploadId = "subsetTitl1";
        _durations[StaticUploadId] = EpisodeLength;
        _durations[subsetUploadId] = TimeSpan.FromMinutes(2);
        _motion.Setup(x => x.DetectAsync(StaticUploadId, It.IsAny<CancellationToken>())).ReturnsAsync(ThumbnailMotion.Static);
        var items = new List<PlaylistItem>
        {
            Item(StaticUploadId, StaticTitle(title)),
            Item(subsetUploadId, "IndoctriNation")
        };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(title), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
        VerifyPreferenceFetched(subsetUploadId, Times.Once());
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When video details for the contenders are unavailable, the pick stands and no thumbnails are fetched.")]
    public async Task missing_contender_details_keep_pick()
    {
        // Arrange: no details for the video upload, so YouTube returns nothing for it.
        const string title = "The Oracle's Daughter with Sarah Green";
        _durations[StaticUploadId] = EpisodeLength;
        var items = new List<PlaylistItem> { Item(StaticUploadId, StaticTitle(title)), Item(VideoUploadId, title) };

        // Act
        var result = await PlaylistSut().FindMatchingYouTubeVideo(Episode(title), items, null, new IndexingContext());

        // Assert
        result!.PlaylistItem!.GetVideoId().Should().Be(StaticUploadId);
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Non-video search results (channels, playlists) are never contenders, so preference does not fetch videos with snippets.")]
    public async Task non_video_search_results_are_ignored()
    {
        // Arrange
        const string plainTitle = "EL-ROI CANNOT SAVE ANYONE";
        _durations[PlainUploadId] = EpisodeLength;
        var results = new List<SearchResult>
        {
            Search(PlainUploadId, plainTitle),
            new()
            {
                Id = new ResourceId { Kind = "youtube#channel", ChannelId = "UC-Vn8JbQ4sbMKYzz7HFMtFg" },
                Snippet = new SearchResultSnippet { Title = plainTitle, PublishedAtDateTimeOffset = Release }
            }
        };

        // Act
        var result = await SearchSut().FindMatchingYouTubeVideo(Episode(plainTitle), results, null, new IndexingContext());

        // Assert
        result!.SearchResult!.Id.VideoId.Should().Be(PlainUploadId);
        _mocker.GetMock<ITolerantYouTubeVideoService>().Verify(
            x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                true,
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Never);
        _motion.Verify(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Preference asks for contenders alone, with snippets. The finder may still load every playlist id together.
    /// </summary>
    private void VerifyPreferenceFetched(string videoId, Times times) =>
        _mocker.GetMock<ITolerantYouTubeVideoService>().Verify(
            x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Contains(videoId) && !ids.Contains(StaticUploadId)),
                It.IsAny<IndexingContext>(),
                true,
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            times);

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