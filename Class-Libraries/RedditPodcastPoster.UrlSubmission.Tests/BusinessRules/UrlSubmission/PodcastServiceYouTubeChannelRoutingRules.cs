using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Which podcast owns a YouTube video when several podcasts (and maybe a news organisation or TV show) share its
/// channel, e.g. Electoral Dysfunction on the Sky News channel. Synthetic ids only.
/// </summary>
public class PodcastServiceYouTubeChannelRoutingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private string _channelId;
    private readonly string _videoId;
    private readonly Uri _url;

    public PodcastServiceYouTubeChannelRoutingRules()
    {
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        _mocker.Use(NonPodcastSubmitAdapterResolverSupport.Create());
        _channelId = _fixture.CreateYouTubeChannelId();
        _videoId = _fixture.CreateYouTubeId();
        _url = new Uri($"https://www.youtube.com/watch?v={_videoId}");
        UseChannel(_channelId);
    }

    private void UseChannel(string channelId)
    {
        _channelId = channelId;
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Contains(_videoId)), It.IsAny<IndexingContext>(), true, false,
                false))
            .ReturnsAsync(new List<Video>
            {
                new() { Id = _videoId, Snippet = new VideoSnippet { ChannelId = channelId, ChannelTitle = "Synthetic Channel" } }
            });
    }

    private async Task<Podcast> AddPodcast(string? playlistId, bool removed = false, string? name = null)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            if (name != null)
            {
                p.Name = name;
            }

            p.YouTubeChannelId = _channelId;
            p.YouTubePlaylistId = playlistId!;
            p.Removed = removed ? true : null;
        });
        await _mocker.Get<IPodcastRepository>().Save(podcast);
        return podcast;
    }

    private void InPlaylist(string playlistId, bool? contains) =>
        _mocker.GetMock<IYouTubePlaylistMembership>()
            .Setup(x => x.Contains(It.IsAny<IYouTubeServiceWrapper>(), playlistId, _videoId))
            .ReturnsAsync(contains);

    private void VerifyContains(string playlistId, Times times) =>
        _mocker.GetMock<IYouTubePlaylistMembership>()
            .Verify(x => x.Contains(It.IsAny<IYouTubeServiceWrapper>(), playlistId, It.IsAny<string>()), times);

    private void ChannelClaimedBy(string? parentPlaylistId)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = _channelId, YouTubePlaylistId = parentPlaylistId }
        };
        _mocker.GetMock<ICatalogueParentLookup>()
            .Setup(x => x.FindByOwner(It.Is<EpisodeUrlOwnerIds>(ids => ids.YouTubeChannelId == _channelId)))
            .ReturnsAsync(CatalogueParentMatch.For(organisation));
    }

    [Fact(DisplayName =
        "When podcasts share a channel, the podcast whose playlist contains the video owns it, not a channel-wide podcast on the same channel.")]
    public async Task playlist_member_beats_channel_wide_podcast()
    {
        // Arrange
        var channelWide = await AddPodcast(string.Empty);
        var playlistA = _fixture.CreateYouTubePlaylistId();
        var playlistB = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(playlistA);
        var owner = await AddPodcast(playlistB);
        InPlaylist(playlistA, false);
        InPlaylist(playlistB, true);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(owner.Id);
        result.Podcast.Id.Should().NotBe(channelWide.Id);
    }

    [Theory(DisplayName =
        "A null youtube-playlist-id is channel-wide exactly like an empty one: it is never treated as a playlist podcast.")]
    [InlineData(null)]
    [InlineData("")]
    public async Task null_and_empty_playlist_are_channel_wide(string? blank)
    {
        // Arrange
        var channelWide = await AddPodcast(blank);
        var playlist = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(playlist);
        InPlaylist(playlist, false);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(channelWide.Id);
    }

    [Fact(DisplayName =
        "A news organisation or TV show that claims the channel channel-wide beats a channel-wide podcast on it: the owner podcast is null so the router picks the parent.")]
    public async Task channel_wide_parent_beats_channel_wide_podcast()
    {
        // Arrange
        await AddPodcast(string.Empty);
        ChannelClaimedBy(null);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast.Should().BeNull();
        result.Ids.YouTubeChannelId.Should().Be(_channelId);
    }

    [Fact(DisplayName =
        "When every podcast on the channel has a playlist and none contains the video, the news organisation or TV show claiming the channel gets it.")]
    public async Task all_playlist_podcasts_miss_falls_to_parent()
    {
        // Arrange
        var playlistA = _fixture.CreateYouTubePlaylistId();
        var playlistB = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(playlistA);
        await AddPodcast(playlistB);
        InPlaylist(playlistA, false);
        InPlaylist(playlistB, false);
        ChannelClaimedBy(null);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "A playlist-scoped news organisation whose playlist does not contain the video does not take it from a channel-wide podcast on the same channel.")]
    public async Task playlist_parent_miss_keeps_channel_wide_podcast()
    {
        // Arrange
        var channelWide = await AddPodcast(string.Empty);
        var parentPlaylist = _fixture.CreateYouTubePlaylistId();
        ChannelClaimedBy(parentPlaylist);
        InPlaylist(parentPlaylist, false);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(channelWide.Id);
    }

    [Fact(DisplayName =
        "Several playlist podcasts on a channel none of which contain the video, with no parent claiming it, is ambiguous rather than a new channel-wide podcast.")]
    public async Task all_playlist_podcasts_miss_without_parent_is_ambiguous()
    {
        // Arrange
        var playlistA = _fixture.CreateYouTubePlaylistId();
        var playlistB = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(playlistA);
        await AddPodcast(playlistB);
        InPlaylist(playlistA, false);
        InPlaylist(playlistB, false);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var act = () => sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not in the playlist of any*");
    }

    [Fact(DisplayName =
        "A playlist membership lookup that fails (null) fails closed instead of guessing an owner.")]
    public async Task unknown_membership_fails_closed()
    {
        // Arrange
        await AddPodcast(string.Empty);
        var playlist = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(playlist);
        InPlaylist(playlist, null);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var act = () => sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Could not check*");
    }

    [Fact(DisplayName =
        "A removed podcast on the channel never competes with a live one, so two channel-wide podcasts where one is removed is not 'multiple podcasts'.")]
    public async Task removed_podcast_is_excluded_from_channel_match()
    {
        // Arrange
        await AddPodcast(string.Empty, removed: true);
        var live = await AddPodcast(string.Empty);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(live.Id);
    }

    [Fact(DisplayName =
        "A removed podcast whose playlist contains the video does not win over the live channel-wide podcast.")]
    public async Task removed_playlist_podcast_does_not_win()
    {
        // Arrange
        var playlist = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(playlist, removed: true);
        InPlaylist(playlist, true);
        var live = await AddPodcast(string.Empty);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(live.Id);
    }

    [Fact(DisplayName =
        "A channel whose only podcast is removed still returns it, so the submit is refused as removed instead of re-creating the podcast.")]
    public async Task removed_only_podcast_is_still_returned()
    {
        // Arrange
        var removed = await AddPodcast(string.Empty, removed: true);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(removed.Id);
        result.Podcast.IsRemoved().Should().BeTrue();
    }

    [Fact(DisplayName =
        "End to end through the router: a channel-wide podcast on a news organisation's channel (the Electoral Dysfunction on Sky News shape) no longer takes the video; the news organisation does.")]
    public async Task router_routes_shared_channel_video_to_parent()
    {
        // Arrange
        await AddPodcast(string.Empty);
        ChannelClaimedBy(null);
        var podcastService = _mocker.CreateInstance<PodcastService>();
        _mocker.Use<IPodcastService>(podcastService);
        var router = _mocker.CreateInstance<EpisodeUrlRouter>();

        // Act
        var route = await router.RouteByOwner(_url, new IndexingContext());

        // Assert
        route.Podcast.Should().BeNull();
        route.Parent!.Kind.Should().Be(CatalogueParentKind.NewsOrganisation);
    }

    [Fact(DisplayName =
        "The Global Story shape: a stored playlist id with a stray '@' on the BBC News channel is skipped with no API call, so the channel-wide BBC News podcast still owns videos outside the other playlists.")]
    public async Task malformed_playlist_does_not_block_shared_channel()
    {
        // Arrange
        UseChannel("UC16niRr50-MSBwiO3YDb3RA");
        const string malformed = "@PLS3XGZxi7cBXo0K8Ts08kgyNwYll5n89O";
        var bbcNews = await AddPodcast(string.Empty, name: "BBC News");
        var globalNews = _fixture.CreateYouTubePlaylistId();
        var newscast = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(globalNews, name: "Global News Podcast");
        await AddPodcast(newscast, name: "Newscast");
        await AddPodcast(malformed, name: "The Global Story");
        InPlaylist(globalNews, false);
        InPlaylist(newscast, false);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(bbcNews.Id);
        VerifyContains(malformed, Times.Never());
    }

    [Fact(DisplayName =
        "A deleted playlist (YouTube 404, read as not containing the video) on one podcast does not block its shared channel: the channel-wide podcast owns the video.")]
    public async Task deleted_playlist_does_not_block_shared_channel()
    {
        // Arrange
        var channelWide = await AddPodcast(string.Empty);
        var deleted = _fixture.CreateYouTubePlaylistId();
        await AddPodcast(deleted);
        InPlaylist(deleted, false);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(channelWide.Id);
    }

    [Fact(DisplayName =
        "Not shared: a channel's only podcast (channel-wide) with no parent owns the video with no membership call.")]
    public async Task single_channel_wide_podcast_needs_no_membership_call()
    {
        // Arrange
        var only = await AddPodcast(string.Empty);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(only.Id);
        _mocker.GetMock<IYouTubePlaylistMembership>().Verify(
            x => x.Contains(It.IsAny<IYouTubeServiceWrapper>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "Not shared: a channel's only podcast (playlist-scoped) with no parent keeps the video with no membership call, because the answer could not change the owner.")]
    public async Task single_playlist_podcast_needs_no_membership_call()
    {
        // Arrange
        var playlist = _fixture.CreateYouTubePlaylistId();
        var only = await AddPodcast(playlist);
        InPlaylist(playlist, false);
        var sut = _mocker.CreateInstance<PodcastService>();

        // Act
        var result = await sut.GetEpisodeUrlOwner(_url, new IndexingContext());

        // Assert
        result.Podcast!.Id.Should().Be(only.Id);
        VerifyContains(playlist, Times.Never());
    }

    [Fact(DisplayName =
        "Through the router, a playlist podcast that contains the video is decided once: one membership call and at most one parent lookup, with no second check by the router.")]
    public async Task router_does_not_recheck_membership()
    {
        // Arrange
        var playlist = _fixture.CreateYouTubePlaylistId();
        var owner = await AddPodcast(playlist);
        InPlaylist(playlist, true);
        ChannelClaimedBy(null);
        _mocker.Use<IPodcastService>(_mocker.CreateInstance<PodcastService>());
        var router = _mocker.CreateInstance<EpisodeUrlRouter>();

        // Act
        var route = await router.RouteByOwner(_url, new IndexingContext());

        // Assert
        route.Podcast!.Id.Should().Be(owner.Id);
        route.Parent.Should().BeNull();
        VerifyContains(playlist, Times.Once());
        _mocker.GetMock<ICatalogueParentLookup>().Verify(x => x.FindByOwner(It.IsAny<EpisodeUrlOwnerIds>()), Times.AtMostOnce);
    }

    [Fact(DisplayName =
        "Through the router, one playlist podcast plus a playlist-scoped news organisation, neither containing the video: the podcast keeps it (legacy rule) and the parent, whose playlist does not contain it, never takes it.")]
    public async Task router_does_not_send_video_to_non_claiming_parent()
    {
        // Arrange
        var playlist = _fixture.CreateYouTubePlaylistId();
        var podcast = await AddPodcast(playlist);
        var parentPlaylist = _fixture.CreateYouTubePlaylistId();
        InPlaylist(playlist, false);
        InPlaylist(parentPlaylist, false);
        ChannelClaimedBy(parentPlaylist);
        _mocker.Use<IPodcastService>(_mocker.CreateInstance<PodcastService>());
        var router = _mocker.CreateInstance<EpisodeUrlRouter>();

        // Act
        var route = await router.RouteByOwner(_url, new IndexingContext());

        // Assert
        route.Podcast!.Id.Should().Be(podcast.Id);
        route.Parent.Should().BeNull();
        VerifyContains(playlist, Times.Once());
        VerifyContains(parentPlaylist, Times.Once());
    }
}
