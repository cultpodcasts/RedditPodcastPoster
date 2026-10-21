using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using SpotifyAPI.Web;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class BlueskyPlatformCardSourceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "A YouTube watch URL becomes a card from the video snippet and thumbnail resolver, because the watch page is not fetched.")]
    public async Task youtube_watch_url_uses_the_video_api()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var image = new Uri($"https://cdn.example.test/{videoId}");
        var watch = new Uri($"https://www.youtube.com/watch?v={videoId}");
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(service => service.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Contains(videoId)),
                It.IsAny<IndexingContext>(),
                true,
                false,
                false))
            .ReturnsAsync(new List<Video>
            {
                new()
                {
                    Id = videoId,
                    Snippet = new VideoSnippet { Title = title, Description = description }
                }
            });
        _mocker.GetMock<IYouTubeThumbnailResolver>()
            .Setup(resolver => resolver.GetUsableCandidateUrls(It.IsAny<Video>()))
            .Returns([image]);
        var sut = _mocker.CreateInstance<BlueskyPlatformCardSource>();

        // Act
        var card = await sut.TryGetAsync(watch);

        // Assert
        card.Should().NotBeNull();
        card!.Link.Should().Be(watch);
        card.Title.Should().Be(title);
        card.Description.Should().Be(description);
        card.ImageUrls.Should().Equal(image);
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Verify(client => client.GetFullEpisode(
                    It.IsAny<string>(),
                    It.IsAny<EpisodeRequest>(),
                    It.IsAny<IndexingContext>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "A YouTube embed URL is sent to the video API as the id Extract returns, because the card does not scrape the embed page.")]
    public async Task youtube_embed_url_uses_the_extracted_id()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var title = _fixture.CreateTitle();
        var image = new Uri($"https://cdn.example.test/{videoId}");
        var embed = new Uri($"https://www.youtube.com/embed/{videoId}");
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(service => service.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Contains(videoId)),
                It.IsAny<IndexingContext>(),
                true,
                false,
                false))
            .ReturnsAsync(new List<Video>
            {
                new() { Id = videoId, Snippet = new VideoSnippet { Title = title, Description = string.Empty } }
            });
        _mocker.GetMock<IYouTubeThumbnailResolver>()
            .Setup(resolver => resolver.GetUsableCandidateUrls(It.IsAny<Video>()))
            .Returns([image]);
        var sut = _mocker.CreateInstance<BlueskyPlatformCardSource>();

        // Act
        var card = await sut.TryGetAsync(embed);

        // Assert
        card.Should().NotBeNull();
        card!.Title.Should().Be(title);
        card.ImageUrls.Should().Equal(image);
    }

    [Fact(DisplayName =
        "A Spotify episode URL becomes a card from the full episode name, description, and images tallest-first, because a smaller image can still be the thumb when the tallest is too large, and the episode page is not fetched.")]
    public async Task spotify_episode_url_uses_the_episode_api()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var small = $"https://cdn.example.test/small-{episodeId}";
        var large = $"https://cdn.example.test/large-{episodeId}";
        var episodeUrl = new Uri($"https://open.spotify.com/episode/{episodeId}");
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(client => client.GetFullEpisode(
                episodeId,
                It.Is<EpisodeRequest>(request => request.Market == Market.CountryCode),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FullEpisode
            {
                Id = episodeId,
                Name = title,
                Description = description,
                Images =
                [
                    new Image { Url = small, Height = 64 },
                    new Image { Url = large, Height = 640 }
                ]
            });
        var sut = _mocker.CreateInstance<BlueskyPlatformCardSource>();

        // Act
        var card = await sut.TryGetAsync(episodeUrl);

        // Assert
        card.Should().NotBeNull();
        card!.Link.Should().Be(episodeUrl);
        card.Title.Should().Be(title);
        card.Description.Should().Be(description);
        card.ImageUrls.Should().Equal(new Uri(large), new Uri(small));
        _mocker.GetMock<IYouTubeVideoService>()
            .Verify(service => service.GetVideoContentDetails(
                    It.IsAny<IYouTubeServiceWrapper>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IndexingContext>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "A host with no YouTube or Spotify API is posted without a card, because the episode page is not scraped.")]
    public async Task other_hosts_do_not_call_either_api()
    {
        // Arrange
        var url = new Uri($"https://example.test/{_fixture.CreateSpotifyId()}");
        var sut = _mocker.CreateInstance<BlueskyPlatformCardSource>();

        // Act
        var card = await sut.TryGetAsync(url);

        // Assert
        card.Should().BeNull();
        _mocker.GetMock<IYouTubeVideoService>()
            .Verify(service => service.GetVideoContentDetails(
                    It.IsAny<IYouTubeServiceWrapper>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IndexingContext>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()),
                Times.Never);
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Verify(client => client.GetFullEpisode(
                    It.IsAny<string>(),
                    It.IsAny<EpisodeRequest>(),
                    It.IsAny<IndexingContext>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "A YouTube lookup with no snippet posts without a card, because there is no page-scrape fallback.")]
    public async Task youtube_miss_does_not_invent_a_card()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var watch = new Uri($"https://www.youtube.com/watch?v={videoId}");
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(service => service.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                true,
                false,
                false))
            .ReturnsAsync((IList<Video>?)null);
        var sut = _mocker.CreateInstance<BlueskyPlatformCardSource>();

        // Act
        var card = await sut.TryGetAsync(watch);

        // Assert
        card.Should().BeNull();
        _mocker.GetMock<IYouTubeThumbnailResolver>()
            .Verify(resolver => resolver.GetUsableCandidateUrls(It.IsAny<Video>()), Times.Never);
    }

    [Fact(DisplayName =
        "A snippet title longer than the embed cap is shortened, because External.Properties does not cap it and the post would be rejected.")]
    public async Task long_youtube_title_is_truncated()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var title = new string('a', BlueskyEmbedText.MaxTitleLength + 8);
        var image = new Uri($"https://cdn.example.test/{videoId}");
        var watch = new Uri($"https://www.youtube.com/watch?v={videoId}");
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(service => service.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                true,
                false,
                false))
            .ReturnsAsync(new List<Video>
            {
                new() { Id = videoId, Snippet = new VideoSnippet { Title = title, Description = string.Empty } }
            });
        _mocker.GetMock<IYouTubeThumbnailResolver>()
            .Setup(resolver => resolver.GetUsableCandidateUrls(It.IsAny<Video>()))
            .Returns([image]);
        var sut = _mocker.CreateInstance<BlueskyPlatformCardSource>();

        // Act
        var card = await sut.TryGetAsync(watch);

        // Assert
        card.Should().NotBeNull();
        card!.Title.Length.Should().Be(BlueskyEmbedText.MaxTitleLength);
        card.Title.Should().EndWith("…");
    }
}
