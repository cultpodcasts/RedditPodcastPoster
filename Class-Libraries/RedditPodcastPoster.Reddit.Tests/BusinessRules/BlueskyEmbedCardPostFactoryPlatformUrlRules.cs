using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class BlueskyEmbedCardPostFactoryPlatformUrlRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private string? _title;
    private string? _podcastName;

    public BlueskyEmbedCardPostFactoryPlatformUrlRules()
    {
        _mocker.Use(Options.Create(new BlueskyOptions
        {
            Identifier = _fixture.Create<string>(),
            Password = _fixture.Create<string>(),
            WithEpisodeUrl = false,
            ShortUrlOnlyWhenShareImage = true,
            ReuseSession = false,
            MaxFailures = 1,
            MaxPosts = 1
        }));
        _mocker.GetMock<ITextSanitiser>()
            .Setup(sanitiser => sanitiser.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(() => _title!);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(sanitiser => sanitiser.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(() => _podcastName!);
        _mocker.GetMock<IHashTagProvider>()
            .Setup(provider => provider.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(resolver => resolver.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact(DisplayName =
        "When a share image replaces the Bluesky link with the short URL, the YouTube URL is kept, because the video API still has to build the card.")]
    public async Task share_image_short_url_keeps_the_youtube_platform_url()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _title = episode.Title;
        _podcastName = podcast.Name;
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), shortUrl, hasShareImage: true);

        // Assert
        post.Url.Should().Be(shortUrl);
        post.UrlService.Should().Be(Service.YouTube);
        post.PlatformUrl.Should().Be(episode.Urls.YouTube);
    }

    [Fact(DisplayName =
        "When a share image replaces the Bluesky link with the short URL, the Spotify URL is kept, because the episode API still has to build the card.")]
    public async Task share_image_short_url_keeps_the_spotify_platform_url()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        _title = episode.Title;
        _podcastName = podcast.Name;
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), shortUrl, hasShareImage: true);

        // Assert
        post.Url.Should().Be(shortUrl);
        post.UrlService.Should().Be(Service.Spotify);
        post.PlatformUrl.Should().Be(episode.Urls.Spotify);
    }
}
