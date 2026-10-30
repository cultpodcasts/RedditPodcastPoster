using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Services;

/// <summary>
/// Matching a known YouTube channel from a Spotify or Apple submit searches a short publish band
/// when Search.List is allowed. A forbidden channel or PreferUploadsPlaylist reads the uploads playlist and keeps only
/// items inside that same band. A podcast with a playlist is matched from its playlist only (inside the band), because
/// the playlist is its only YouTube source.
/// </summary>
public class YouTubeSubmitMatchBandRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeSubmitMatchBandRules()
    {
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(new List<SearchResult>());
    }

    [Fact(DisplayName =
        "When the podcast has a YouTube channel and no playlist, Resolve searches that channel between the submit-match band edges " +
        "shifted by the publishing delay, and does not walk a playlist, because the audio date is a clue for the video date.")]
    public async Task Known_channel_searches_the_band_around_audio_plus_delay()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var delay = TimeSpan.FromDays(3);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
            p.YouTubePublicationOffset = delay.Ticks;
        });
        DateTimeOffset? publishedAfter = null;
        DateTimeOffset? publishedBefore = null;
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                channelId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .Callback<string, DateTimeOffset, DateTimeOffset, IndexingContext>((_, after, before, _) =>
            {
                publishedAfter = after;
                publishedBefore = before;
            })
            .ReturnsAsync(new List<SearchResult>());
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();
        var expectedPublish = release.Add(delay);
        var band = EpisodeReleaseTolerance.GetSubmitMatchBand(expectedPublish);

        // Act
        var result = await sut.Resolve(CreateCriteria(release), podcast, [], new IndexingContext());

        // Assert
        result.Should().BeNull();
        publishedAfter.Should().Be(new DateTimeOffset(DateTime.SpecifyKind(band.Start, DateTimeKind.Utc)));
        publishedBefore.Should().Be(new DateTimeOffset(DateTime.SpecifyKind(band.End.AddDays(1), DateTimeKind.Utc)));
        _mocker.GetMock<ITolerantYouTubePlaylistService>().Verify(
            x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the audio release is years old and the channel is known, Resolve still bounds publishedBefore to the band " +
        "because a 2024 episode must not search the channel through to today.")]
    public async Task Years_old_audio_does_not_search_through_to_now()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var release = DomainTestFixture.UtcDateDaysAgo(800);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePublicationOffset = 0;
        });
        DateTimeOffset? publishedBefore = null;
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                channelId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .Callback<string, DateTimeOffset, DateTimeOffset, IndexingContext>((_, _, before, _) =>
                publishedBefore = before)
            .ReturnsAsync(new List<SearchResult>());
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();
        var band = EpisodeReleaseTolerance.GetSubmitMatchBand(release);

        // Act
        await sut.Resolve(CreateCriteria(release), podcast, [], new IndexingContext());

        // Assert
        publishedBefore.Should().Be(new DateTimeOffset(DateTime.SpecifyKind(band.End.AddDays(1), DateTimeKind.Utc)));
        publishedBefore!.Value.UtcDateTime.Should().BeBefore(DateTime.UtcNow.AddDays(-700));
    }

    [Fact(DisplayName =
        "When the podcast has no YouTube channel or playlist, Resolve does not search YouTube " +
        "because a Spotify or Apple URL cannot be matched to a video without a YouTube source.")]
    public async Task Missing_channel_does_not_search_youtube()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(
            CreateCriteria(DomainTestFixture.UtcDateDaysAgo(1)),
            podcast,
            [],
            new IndexingContext());

        // Assert
        result.Should().BeNull();
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>().Verify(
            x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Theory(DisplayName =
        "When the podcast has a playlist, Resolve returns the in-band playlist hit and does not call channel search, " +
        "whatever the channel's Search.List policy, because the playlist is the podcast's only YouTube source.")]
    [InlineData("youTubeChannelSearchForbidden")]
    [InlineData("PreferUploadsPlaylist")]
    [InlineData(null)]
    public async Task Playlist_podcast_returns_in_band_playlist_hit_without_channel_search(string? reason)
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var release = DomainTestFixture.UtcDateDaysAgo(10);
        var title = _fixture.CreateTitle();
        var inBandVideoId = _fixture.CreateYouTubeId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = playlistId;
            p.YouTubePublicationOffset = 0;
        });
        var band = EpisodeReleaseTolerance.GetSubmitMatchBand(release);
        var publishedAfter = new DateTimeOffset(DateTime.SpecifyKind(band.Start, DateTimeKind.Utc));
        IndexingContext? capturedContext = null;
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(podcast))
            .Returns(reason);
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .Callback<YouTubePlaylistId, IndexingContext, bool, bool, PlaylistOrder?>((_, ctx, _, _, _) =>
                capturedContext = ctx)
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
            [
                CreatePlaylistItem(inBandVideoId, channelId, title, new DateTimeOffset(release)),
                CreatePlaylistItem(
                    _fixture.CreateYouTubeId(),
                    channelId,
                    title,
                    publishedAfter.AddDays(-2))
            ]));
        StubVideoDetails();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(CreateCriteria(release, title), podcast, [], new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.EpisodeId.Should().Be(inBandVideoId);
        capturedContext.Should().NotBeNull();
        capturedContext!.ReleasedSince.Should().Be(publishedAfter.UtcDateTime);
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>().Verify(
            x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the channel has no stored playlist, a PreferUploadsPlaylist submit reads the uploads playlist inside the band " +
        "and returns that hit, because Search.List is skipped for that policy.")]
    public async Task Uploads_playlist_fallback_returns_in_band_hit()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var uploadsPlaylistId = _fixture.CreateYouTubePlaylistId();
        var release = DomainTestFixture.UtcDateDaysAgo(12);
        var title = _fixture.CreateTitle();
        var inBandVideoId = _fixture.CreateYouTubeId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
            p.YouTubePublicationOffset = 0;
        });
        YouTubePlaylistId? capturedPlaylist = null;
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(podcast))
            .Returns("PreferUploadsPlaylist");
        _mocker.GetMock<ITolerantYouTubeChannelService>()
            .Setup(x => x.GetChannel(
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(CreateChannel(uploadsPlaylistId));
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .Callback<YouTubePlaylistId, IndexingContext, bool, bool, PlaylistOrder?>((id, _, _, _, _) =>
                capturedPlaylist = id)
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
            [
                CreatePlaylistItem(inBandVideoId, channelId, title, new DateTimeOffset(release))
            ]));
        StubVideoDetails();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(CreateCriteria(release, title), podcast, [], new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.EpisodeId.Should().Be(inBandVideoId);
        capturedPlaylist.Should().NotBeNull();
        capturedPlaylist!.PlaylistId.Should().Be(uploadsPlaylistId);
        capturedPlaylist.Source.Should().Be(YouTubePlaylistIdSource.ChannelUploads);
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>().Verify(
            x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Search.List is account-delegation forbidden, Resolve stores that on the podcast and returns the in-band playlist hit " +
        "because the channel can still be read from its playlist.")]
    public async Task Search_forbidden_records_the_channel_and_returns_the_playlist_hit()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var release = DomainTestFixture.UtcDateDaysAgo(8);
        var title = _fixture.CreateTitle();
        var inBandVideoId = _fixture.CreateYouTubeId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
            p.YouTubePublicationOffset = 0;
            p.YouTubeChannelSearchForbidden = null;
        });
        _mocker.GetMock<ITolerantYouTubeChannelService>()
            .Setup(x => x.GetChannel(
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(CreateChannel(_fixture.CreateYouTubePlaylistId()));
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(podcast))
            .Returns((string?)null);
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                channelId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .ThrowsAsync(new YouTubeChannelSearchForbiddenException(
                channelId,
                new InvalidOperationException("accountDelegationForbidden")));
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
            [
                CreatePlaylistItem(inBandVideoId, channelId, title, new DateTimeOffset(release))
            ]));
        StubVideoDetails();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(CreateCriteria(release, title), podcast, [], new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.EpisodeId.Should().Be(inBandVideoId);
        podcast.YouTubeChannelSearchForbidden.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the playlist fallback's only video is newer than the submit-match band, Resolve returns nothing " +
        "because a hit outside the band is not a match for that episode.")]
    public async Task Playlist_item_newer_than_the_band_is_not_returned()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var release = DomainTestFixture.UtcDateDaysAgo(30);
        var title = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePublicationOffset = 0;
        });
        var band = EpisodeReleaseTolerance.GetSubmitMatchBand(release);
        var newerThanBand = new DateTimeOffset(DateTime.SpecifyKind(band.End.AddDays(2), DateTimeKind.Utc));
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(podcast))
            .Returns("youTubeChannelSearchForbidden");
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
            [
                CreatePlaylistItem(_fixture.CreateYouTubeId(), channelId, title, newerThanBand)
            ]));
        StubVideoDetails();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(CreateCriteria(release, title), podcast, [], new IndexingContext());

        // Assert
        result.Should().BeNull();
        _mocker.GetMock<ITolerantYouTubeVideoService>().Verify(
            x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A playlist podcast with no channel id is matched from its playlist, including a video another channel " +
        "uploaded, with the podcast's playlist order, because the playlist alone is its YouTube source.")]
    public async Task Playlist_without_channel_is_matched_from_the_playlist()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var uploaderChannelId = _fixture.CreateYouTubeChannelId();
        var release = DomainTestFixture.UtcDateDaysAgo(5);
        var title = _fixture.CreateTitle();
        var videoId = _fixture.CreateYouTubeId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = playlistId;
            p.YouTubePlaylistOrder = PlaylistOrder.Arbitrary;
            p.YouTubePublicationOffset = 0;
        });
        YouTubePlaylistId? capturedPlaylist = null;
        PlaylistOrder? capturedOrder = null;
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .Callback<YouTubePlaylistId, IndexingContext, bool, bool, PlaylistOrder?>((id, _, _, _, order) =>
            {
                capturedPlaylist = id;
                capturedOrder = order;
            })
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
            [
                CreatePlaylistItem(videoId, uploaderChannelId, title, new DateTimeOffset(release))
            ]));
        StubVideoDetails();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(CreateCriteria(release, title), podcast, [], new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.EpisodeId.Should().Be(videoId);
        capturedPlaylist!.PlaylistId.Should().Be(playlistId);
        capturedPlaylist.Source.Should().Be(YouTubePlaylistIdSource.PodcastEntity);
        capturedOrder.Should().Be(PlaylistOrder.Arbitrary);
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>().Verify(
            x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A playlist video already stored on the podcast is not matched again, because the playlist item id is not the " +
        "video id and the stored ids are compared with the video id.")]
    public async Task Stored_playlist_video_is_not_matched_again()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(5);
        var title = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePublicationOffset = 0;
        });
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var videoId = EpisodeServicePresence.YouTubeEpisodeId(stored)!;
        var item = CreatePlaylistItem(videoId, _fixture.CreateYouTubeChannelId(), title, new DateTimeOffset(release));
        item.Id = "playlist-item-" + videoId;
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse([item]));
        StubVideoDetails();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(CreateCriteria(release, title), podcast, [stored], new IndexingContext());

        // Assert
        result.Should().BeNull();
    }

    private PodcastServiceSearchCriteria CreateCriteria(DateTime release, string? episodeTitle = null) =>
        new(
            ShowName: _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: episodeTitle ?? _fixture.CreateTitle(),
            EpisodeDescription: _fixture.CreateTitle(),
            Release: release,
            Duration: _fixture.CreateDuration());

    private void StubVideoDetails() =>
        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new List<Google.Apis.YouTube.v3.Data.Video>());

    private Google.Apis.YouTube.v3.Data.Channel CreateChannel(string uploadsPlaylistId) =>
        new()
        {
            Snippet = new ChannelSnippet { Description = _fixture.CreateTitle() },
            ContentOwnerDetails = new ChannelContentOwnerDetails { ContentOwner = _fixture.CreateTitle() },
            ContentDetails = new ChannelContentDetails
            {
                RelatedPlaylists = new ChannelContentDetails.RelatedPlaylistsData { Uploads = uploadsPlaylistId }
            }
        };

    private static PlaylistItem CreatePlaylistItem(
        string videoId,
        string channelId,
        string title,
        DateTimeOffset published) =>
        new()
        {
            Id = videoId,
            Snippet = new PlaylistItemSnippet
            {
                Title = title,
                Description = title,
                ChannelId = channelId,
                ChannelTitle = title,
                PublishedAtDateTimeOffset = published,
                ResourceId = new ResourceId { VideoId = videoId }
            }
        };
}
