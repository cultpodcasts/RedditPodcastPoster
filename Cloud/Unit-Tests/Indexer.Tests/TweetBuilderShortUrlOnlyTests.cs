using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Common.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Configuration;
using Xunit;

namespace Indexer.Tests;

public class TweetBuilderShortUrlOnlyTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When shortener KV has a share image, the tweet body uses only the short URL and omits platform links.")]
    public async Task Has_share_image_tweet_is_short_url_only()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var platformHost = episode.Urls.YouTube!.Host;
        var sut = CreateSut(withEpisodeUrl: false);

        // Act
        var tweet = await sut.BuildTweet(podcastEpisode, shortUrl, hasShareImage: true);

        // Assert
        tweet.Should().Contain(shortUrl.ToString());
        tweet.Should().NotContain(platformHost);
    }

    [Fact(DisplayName =
        "When shortener KV has no share image, the tweet body still includes the primary platform URL.")]
    public async Task No_share_image_tweet_includes_platform_url()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var platformUrl = episode.Urls.YouTube!;
        var sut = CreateSut(withEpisodeUrl: false);

        // Act
        var tweet = await sut.BuildTweet(podcastEpisode, shortUrl, hasShareImage: false);

        // Assert
        tweet.Should().Contain(platformUrl.ToString());
    }

    private TweetBuilder CreateSut(bool withEpisodeUrl)
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

        var options = Options.Create(new TwitterOptions
        {
            ConsumerKey = "k",
            ConsumerSecret = "s",
            AccessToken = "t",
            AccessTokenSecret = "ts",
            WithEpisodeUrl = withEpisodeUrl
        });

        return new TweetBuilder(
            textSanitiser.Object,
            hashTagEnricher.Object,
            hashTagProvider.Object,
            postModelFactory.Object,
            personGuestHandleResolver.Object,
            options,
            Mock.Of<ILogger<TweetBuilder>>());
    }
}
