using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class TweetBuilderEpisodeHashTagRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "TweetBuilder: when episode HashTag is space-delimited multi-tag, then BuildTweet end hashtags include each tag from ToHashTags, because episode hashtags append as end tags.")]
    public async Task build_tweet_includes_multi_episode_hash_tags_as_end_tags()
    {
        // Arrange
        const string episodeHashTag = "#ABC #XYZ";
        var expectedTags = episodeHashTag.ToHashTags().Select(x => $"#{x.Tag.TrimStart('#')}").ToArray();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.TwitterHandle = string.Empty;
        });
        var title = _fixture.CreateTitle();
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = episodeHashTag;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        var podcastEpisode = new PodcastEpisode(podcast, episode);

        var textSanitiser = new Mock<ITextSanitiser>();
        textSanitiser.Setup(x => x.SanitiseTitle(It.IsAny<PostModel>())).ReturnsAsync(title);
        textSanitiser.Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>())).Returns(podcast.Name);

        var hashTagEnricher = new Mock<IHashTagEnricher>();
        hashTagEnricher
            .Setup(x => x.AddHashTag(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((string input, string _, string? _) => (input, false));

        var hashTagProvider = new Mock<IHashTagProvider>();
        hashTagProvider.Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());

        var postModelFactory = new Mock<IPostModelFactory>();
        postModelFactory
            .Setup(x => x.ToPostModel(It.IsAny<(RedditPodcastPoster.Models.Podcasts.Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<RedditPodcastPoster.Models.Podcasts.Podcast, IEnumerable<Episode>> pe, bool _) =>
                new PostModel(
                    pe.Item1.Name,
                    string.Empty,
                    string.Empty,
                    [
                        new EpisodePost(
                            pe.Item2.First().Title,
                            pe.Item2.First().Urls.YouTube,
                            pe.Item2.First().Urls.Spotify,
                            pe.Item2.First().Urls.Apple,
                            "1 Jan 2020",
                            "01:00:00",
                            pe.Item2.First().Description,
                            pe.Item2.First().Id.ToString(),
                            pe.Item2.First().Release,
                            pe.Item2.First().Subjects.ToArray(),
                            pe.Item2.First().Urls.BBC,
                            pe.Item2.First().Urls.InternetArchive)
                    ],
                    null,
                    [],
                    []));

        var guestResolver = new Mock<IPersonGuestHandleResolver>();
        guestResolver.Setup(x => x.Resolve(It.IsAny<Episode>())).ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));

        var options = Options.Create(new TwitterOptions
        {
            ConsumerKey = "k",
            ConsumerSecret = "s",
            AccessToken = "t",
            AccessTokenSecret = "ts",
            HashTag = null,
            WithEpisodeUrl = false
        });

        var sut = new TweetBuilder(
            textSanitiser.Object,
            hashTagEnricher.Object,
            hashTagProvider.Object,
            postModelFactory.Object,
            guestResolver.Object,
            options,
            Mock.Of<ILogger<TweetBuilder>>());

        // Act
        var tweet = await sut.BuildTweet(podcastEpisode, null);

        // Assert
        foreach (var tag in expectedTags)
        {
            tweet.Should().Contain(tag);
        }
    }
}
