using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Common.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;

namespace Indexer.Tests;

public class BlueskyEmbedCardPostFactoryShortUrlOnlyTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When shortener KV has a share image, Bluesky embed URL is the short URL while UrlService stays YouTube for thumb fetch.")]
    public async Task Has_share_image_embed_url_is_short_url_service_youtube()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var sut = CreateSut(withEpisodeUrl: false);

        // Act
        var post = await sut.Create(podcastEpisode, shortUrl, hasShareImage: true);

        // Assert
        post.Url.Should().Be(shortUrl);
        post.UrlService.Should().Be(Service.YouTube);
        post.Text.Should().Contain(shortUrl.ToString());
        post.Text.Should().NotContain(episode.Urls.YouTube!.Host);
    }

    [Fact(DisplayName =
        "When shortener KV has no share image, Bluesky embed URL remains the primary platform URL.")]
    public async Task No_share_image_embed_url_is_platform()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var sut = CreateSut(withEpisodeUrl: false);

        // Act
        var post = await sut.Create(podcastEpisode, shortUrl, hasShareImage: false);

        // Assert
        post.Url.Should().Be(episode.Urls.YouTube);
        post.UrlService.Should().Be(Service.YouTube);
    }

    [Fact(DisplayName =
        "When shortener KV has a share image for a Spotify-primary episode, UrlService stays Spotify for thumb fetch.")]
    public async Task Has_share_image_spotify_episode_keeps_spotify_url_service()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var sut = CreateSut(withEpisodeUrl: false);

        // Act
        var post = await sut.Create(podcastEpisode, shortUrl, hasShareImage: true);

        // Assert
        post.Url.Should().Be(shortUrl);
        post.UrlService.Should().Be(Service.Spotify);
    }

    [Fact(DisplayName =
        "When shortener KV has a share image, the Bluesky title can be longer because only one URL is in the post text.")]
    public async Task Has_share_image_allows_longer_bluesky_title_than_dual_url_budget()
    {
        // Arrange — length literal is the truncation boundary under test
        var longTitle = new string('x', 280);
        var podcast = _fixture.CreatePodcast(p => p.Name = "Show");
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, title: longTitle);
        episode.Subjects = ["Subject"];
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var sut = CreateSut(withEpisodeUrl: true);

        // Act
        var shortOnly = await sut.Create(podcastEpisode, shortUrl, hasShareImage: true);
        var withSecondUrlBudget = await sut.Create(podcastEpisode, shortUrl, hasShareImage: false);

        // Assert
        QuotedTitleLength(shortOnly.Text).Should().BeGreaterThan(QuotedTitleLength(withSecondUrlBudget.Text));
    }

    private static int QuotedTitleLength(string post)
    {
        var start = post.IndexOf('"') + 1;
        var end = post.IndexOf('"', start);
        return end - start;
    }

    private BlueskyEmbedCardPostFactory CreateSut(bool withEpisodeUrl)
    {
        var textSanitiser = new Mock<ITextSanitiser>();
        textSanitiser
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync((PostModel pm) => pm.EpisodeTitle);
        textSanitiser
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns((PostModel pm) => pm.PodcastName);

        var hashTagEnricher = new Mock<IHashTagEnricher>();
        hashTagEnricher
            .Setup(x => x.AddHashTag(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((string input, string _, string? _) => (input, false));

        var hashTagProvider = new Mock<IHashTagProvider>();
        hashTagProvider
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());

        var postModelFactory = new Mock<IPostModelFactory>();
        postModelFactory
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((
                (Podcast Podcast, IEnumerable<Episode> Episodes) pe,
                bool _) => new PostModel(
                pe.Podcast.Name,
                string.Empty,
                string.Empty,
                pe.Episodes.Select(e => new EpisodePost(
                    e.Title,
                    e.Urls.YouTube,
                    e.Urls.Spotify,
                    e.Urls.Apple,
                    e.Release.ToString("d MMM yyyy"),
                    e.Length.ToString(@"\[h\:mm\:ss\]"),
                    e.Description,
                    e.Id.ToString(),
                    e.Release,
                    e.Subjects.ToArray(),
                    e.Urls.BBC,
                    e.Urls.InternetArchive)),
                null,
                [],
                []));

        var personGuestHandleResolver = new Mock<IPersonGuestHandleResolver>();
        personGuestHandleResolver
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));

        var options = Options.Create(new BlueskyOptions
        {
            Identifier = "id",
            Password = "pw",
            WithEpisodeUrl = withEpisodeUrl,
            ReuseSession = false,
            MaxFailures = 1,
            MaxPosts = 1
        });

        return new BlueskyEmbedCardPostFactory(
            textSanitiser.Object,
            hashTagEnricher.Object,
            hashTagProvider.Object,
            postModelFactory.Object,
            personGuestHandleResolver.Object,
            options,
            Mock.Of<ILogger<IBlueskyEmbedCardPostFactory>>());
    }
}
