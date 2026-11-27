using System.Xml;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Services;

/// <summary>
/// Spotify/Apple submit enrich must use <see cref="IPlaylistItemFinder"/> (same duration +
/// <see cref="CrossPlatformMatchScorer"/> path as index enrich). A short promo clip whose title
/// embeds the guest name must lose to the full-length upload.
/// </summary>
public class YouTubeSubmitClipVsFullEpisodeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Dictionary<string, TimeSpan> _durations = new(StringComparer.Ordinal);
    private Uri? _thumbnailUrl;

    public YouTubeSubmitClipVsFullEpisodeRules()
    {
        _mocker.Use(EpisodeDomainTestServices.CreatePlatformMatcher());
        _mocker.Use<IPlaylistItemFinder>(_mocker.CreateInstance<PlaylistItemFinder>());
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(new List<SearchResult>());
        // Mirror production: duration matching loads contentDetails only; snippet (thumbnails) only when requested.
        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((
                IYouTubeServiceWrapper _,
                IEnumerable<string> ids,
                IndexingContext _,
                bool withSnippets,
                bool _,
                bool _) =>
                ids.Where(id => _durations.ContainsKey(id))
                    .Select(id => CreateVideo(id, _durations[id], withSnippets))
                    .ToList());
        _mocker.GetMock<IYouTubeThumbnailResolver>()
            .Setup(x => x.GetImageUrlAsync(
                It.IsAny<Google.Apis.YouTube.v3.Data.Video?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Google.Apis.YouTube.v3.Data.Video? video, CancellationToken _) =>
                video?.Snippet is null ? null : _thumbnailUrl);
        _mocker.GetMock<ITolerantYouTubeChannelService>()
            .Setup(x => x.GetChannel(
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new Google.Apis.YouTube.v3.Data.Channel
            {
                Snippet = new ChannelSnippet { Description = _fixture.CreateTitle() },
                ContentOwnerDetails = new ChannelContentOwnerDetails
                {
                    ContentOwner = _fixture.CreateTitle()
                }
            });
    }

    [Fact(DisplayName =
        "When Spotify submit enrich finds a short YouTube promo clip whose title embeds the audio guest title " +
        "and a full-length upload with a marketing title that does not, Resolve picks the full-length upload via " +
        "IPlaylistItemFinder (duration + CrossPlatformMatchScorer), not title-only fuzzy match.")]
    public async Task long_form_spotify_submit_prefers_full_episode_over_title_matching_clip()
    {
        // Arrange — guest-named audio + clip title containing guest vs marketing full title.
        var guestName = _fixture.CreateTitle(2);
        var audioTitle = guestName;
        var clipTitle = $"How {guestName} {_fixture.CreateTitle(4)} | {_fixture.CreateTitle(2)}";
        var fullTitle = _fixture.CreateTitleNotContainedIn($"{audioTitle} {clipTitle}");
        FuzzyMatcher.Score(audioTitle, clipTitle).Should().BeGreaterThan(
            FuzzyMatcher.Score(audioTitle, fullTitle),
            because: "the bug requires the clip title to outscore the full episode on fuzzy alone");

        var episodeLength = TimeSpan.FromMinutes(98);
        var clipLength = TimeSpan.FromMinutes(9);
        var fullLength = episodeLength + TimeSpan.FromSeconds(40);
        YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(episodeLength, clipLength).Should().BeFalse();
        YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(episodeLength, fullLength).Should().BeTrue();

        var channelId = _fixture.CreateYouTubeChannelId();
        var clipVideoId = _fixture.CreateYouTubeId();
        var fullVideoId = _fixture.CreateYouTubeId();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var published = new DateTimeOffset(release);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
            p.YouTubePublicationOffset = 0;
        });
        _durations[clipVideoId] = clipLength;
        _durations[fullVideoId] = fullLength;
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                channelId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(
            [
                CreateSearchResult(clipVideoId, channelId, clipTitle, published),
                CreateSearchResult(fullVideoId, channelId, fullTitle, published)
            ]);
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();
        var criteria = new PodcastServiceSearchCriteria(
            ShowName: _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: audioTitle,
            EpisodeDescription: _fixture.CreateTitle(),
            Release: release,
            Duration: episodeLength);

        // Act
        var result = await sut.Resolve(criteria, podcast, [], new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.EpisodeId.Should().Be(fullVideoId);
        result.EpisodeId.Should().NotBe(clipVideoId);
    }

    [Fact(DisplayName =
        "When PlaylistItemFinder matches a contentDetails-only YouTube video (no snippet) during Spotify submit enrich, " +
        "Resolve re-fetches that video with snippets and attaches a non-null image URL from the thumbnail resolver, " +
        "because thumbnails live on snippet and the prior submit path always fetched withSnippets.")]
    public async Task spotify_submit_enrich_refetches_snippets_for_matched_video_image()
    {
        // Arrange — contentDetails-only match path; image only after withSnippets re-fetch.
        var audioTitle = _fixture.CreateTitle();
        var episodeLength = _fixture.CreateDuration();
        var channelId = _fixture.CreateYouTubeChannelId();
        var videoId = _fixture.CreateYouTubeId();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var published = new DateTimeOffset(release);
        var imageUrl = _fixture.Create<Uri>();
        _thumbnailUrl = imageUrl;
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
            p.YouTubePublicationOffset = 0;
        });
        _durations[videoId] = episodeLength;
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                channelId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([CreateSearchResult(videoId, channelId, audioTitle, published)]);
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();
        var criteria = new PodcastServiceSearchCriteria(
            ShowName: _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: audioTitle,
            EpisodeDescription: _fixture.CreateTitle(),
            Release: release,
            Duration: episodeLength);

        // Act
        var result = await sut.Resolve(criteria, podcast, [], new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.EpisodeId.Should().Be(videoId);
        result.Image.Should().Be(imageUrl);
        _mocker.GetMock<ITolerantYouTubeVideoService>().Verify(
            x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Contains(videoId)),
                It.IsAny<IndexingContext>(),
                true,
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.AtLeastOnce);
    }

    private static SearchResult CreateSearchResult(
        string videoId,
        string channelId,
        string title,
        DateTimeOffset published) =>
        new()
        {
            Id = new ResourceId { VideoId = videoId, Kind = "youtube#video" },
            Snippet = new SearchResultSnippet
            {
                Title = title,
                Description = title,
                ChannelId = channelId,
                ChannelTitle = channelId,
                PublishedAtDateTimeOffset = published
            }
        };

    private static Google.Apis.YouTube.v3.Data.Video CreateVideo(
        string videoId,
        TimeSpan length,
        bool withSnippets) =>
        new()
        {
            Id = videoId,
            Snippet = withSnippets ? new VideoSnippet { Title = videoId } : null,
            ContentDetails = new VideoContentDetails
            {
                Duration = XmlConvert.ToString(length),
                ContentRating = new ContentRating()
            }
        };
}
