using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class PodcastServiceNonPodcastLookupRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PodcastServiceNonPodcastLookupRules()
    {
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        _mocker.Use(NonPodcastSubmitAdapterResolverSupport.Create());
    }

    [Fact(DisplayName =
        "A BBC Sounds play URL does not resolve a series from catalogue show ids, " +
        "because Sounds pages are not a podcast feed — submit creates or uses an explicit series instead.")]
    public async Task sounds_url_does_not_resolve_a_podcast_from_episode_url()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastService>();
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

        // Act
        var podcast = await sut.GetPodcastFromEpisodeUrl(url, new IndexingContext());

        // Assert
        podcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "An Internet Archive details URL does not resolve a series from catalogue show ids.")]
    public async Task archive_url_does_not_resolve_a_podcast_from_episode_url()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastService>();
        var url = new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}");

        // Act
        var podcast = await sut.GetPodcastFromEpisodeUrl(url, new IndexingContext());

        // Assert
        podcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "A Vimeo URL does not resolve a series from catalogue show ids, " +
        "because Vimeo pages are not a podcast feed — submit creates or uses an explicit series instead.")]
    public async Task vimeo_url_does_not_resolve_a_podcast_from_episode_url()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastService>();
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");

        // Act
        var podcast = await sut.GetPodcastFromEpisodeUrl(url, new IndexingContext());

        // Assert
        podcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "GetEpisodeUrlOwner returns the Apple podcast id it matched on, with the owning podcast when one has it and none otherwise, so a news organisation or TV show can be looked up.")]
    public async Task episode_url_owner_carries_apple_podcast_id()
    {
        // Arrange
        var appleId = _fixture.CreateAppleId();
        var podcast = _fixture.CreatePodcast(p => p.AppleId = appleId);
        await _mocker.Get<IPodcastRepository>().Save(podcast);
        var sut = _mocker.CreateInstance<PodcastService>();
        var owned = new Uri($"https://podcasts.apple.com/us/podcast/synthetic/id{appleId}?i={_fixture.CreateAppleId()}");
        var unowned = new Uri($"https://podcasts.apple.com/us/podcast/synthetic/id{appleId + 1}?i={_fixture.CreateAppleId()}");
        var bbc = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

        // Act
        var ownedResult = await sut.GetEpisodeUrlOwner(owned, new IndexingContext());
        var unownedResult = await sut.GetEpisodeUrlOwner(unowned, new IndexingContext());
        var bbcResult = await sut.GetEpisodeUrlOwner(bbc, new IndexingContext());

        // Assert
        ownedResult.Podcast!.Id.Should().Be(podcast.Id);
        ownedResult.Ids.ApplePodcastId.Should().Be(appleId);
        unownedResult.Podcast.Should().BeNull();
        unownedResult.Ids.ApplePodcastId.Should().Be(appleId + 1);
        bbcResult.Ids.IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Playlist membership: a channel-wide podcast always contains the video, a list= URL of its playlist needs no API call, otherwise YouTube's answer (including unknown) is returned.")]
    public async Task playlist_membership_rules()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var videoId = _fixture.CreateYouTubeId();
        var scoped = _fixture.CreatePodcast(p => p.YouTubePlaylistId = playlistId);
        var channelWide = _fixture.CreatePodcast(p => p.YouTubePlaylistId = string.Empty);
        var membership = _mocker.GetMock<RedditPodcastPoster.PodcastServices.YouTube.Playlist.IYouTubePlaylistMembership>();
        membership.SetupSequence(m => m.Contains(
                It.IsAny<RedditPodcastPoster.PodcastServices.YouTube.Clients.IYouTubeServiceWrapper>(), playlistId, videoId))
            .ReturnsAsync(false)
            .ReturnsAsync((bool?)null);
        var sut = _mocker.CreateInstance<PodcastService>();
        var plain = new Uri($"https://www.youtube.com/watch?v={videoId}");
        var withList = new Uri($"https://www.youtube.com/watch?v={videoId}&list={playlistId}");

        // Act
        var wide = await sut.IsVideoInPodcastPlaylist(channelWide, plain, new IndexingContext());
        var listed = await sut.IsVideoInPodcastPlaylist(scoped, withList, new IndexingContext());
        var notIn = await sut.IsVideoInPodcastPlaylist(scoped, plain, new IndexingContext());
        var unknown = await sut.IsVideoInPodcastPlaylist(scoped, plain, new IndexingContext());

        // Assert
        wide.Should().BeTrue();
        listed.Should().BeTrue();
        notIn.Should().BeFalse();
        unknown.Should().BeNull();
        membership.Verify(m => m.Contains(
            It.IsAny<RedditPodcastPoster.PodcastServices.YouTube.Clients.IYouTubeServiceWrapper>(), playlistId, videoId),
            Times.Exactly(2));
    }
}
