using FluentAssertions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Services;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Services;

public class YouTubeChannelVideoRetrievalPolicyTests
{
    [Fact]
    public void ShouldUseUploadsPlaylist_WhenSearchForbidden_ReturnsTrue()
    {
        var podcast = new Podcast { YouTubeChannelSearchForbidden = true };
        var sut = CreateSut(preferUploadsPlaylist: false);

        sut.ShouldUseUploadsPlaylist(podcast).Should().BeTrue();
    }

    [Fact]
    public void ShouldUseUploadsPlaylist_WhenFeatureFlagEnabled_ReturnsTrue()
    {
        var podcast = new Podcast();
        var sut = CreateSut(preferUploadsPlaylist: true);

        sut.ShouldUseUploadsPlaylist(podcast).Should().BeTrue();
    }

    [Fact]
    public void ShouldUseUploadsPlaylist_WhenNeitherSet_ReturnsFalse()
    {
        var podcast = new Podcast();
        var sut = CreateSut(preferUploadsPlaylist: false);

        sut.ShouldUseUploadsPlaylist(podcast).Should().BeFalse();
        sut.GetUploadsPlaylistReason(podcast).Should().BeNull();
    }

    [Fact]
    public void GetUploadsPlaylistReason_WhenSearchForbidden_ReturnsExpectedReason()
    {
        var podcast = new Podcast { YouTubeChannelSearchForbidden = true };
        var sut = CreateSut(preferUploadsPlaylist: false);

        sut.GetUploadsPlaylistReason(podcast).Should().Be("youTubeChannelSearchForbidden");
    }

    private static YouTubeChannelVideoRetrievalPolicy CreateSut(bool preferUploadsPlaylist) =>
        new(Options.Create(new YouTubeChannelOptions { PreferUploadsPlaylist = preferUploadsPlaylist }));
}
