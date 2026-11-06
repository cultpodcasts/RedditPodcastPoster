using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Tests.BusinessRules;

public class TweetBuilderSymbolHashTagRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TweetBuilderSymbolHashTagRules()
    {
        _mocker.Use<IHashTagEnricher>(new HashTagEnricher());
        _mocker.Use(Options.Create(new TwitterOptions
        {
            ConsumerKey = "k",
            ConsumerSecret = "s",
            AccessToken = "t",
            AccessTokenSecret = "ts",
            HashTag = null,
            WithEpisodeUrl = false
        }));
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
    }

    [Theory(DisplayName =
        "TweetBuilder: a stored tag that either platform would shorten is left out of the post, because the shortened tag is not the stored tag.")]
    [InlineData("Notes on C++ today", "C++")]
    [InlineData("Community LGBTQ+ meetup notes", "LGBTQ+")]
    [InlineData("The Q&A hour starts now", "Q&A")]
    public async Task build_tweet_leaves_out_a_tag_that_cannot_be_reproduced_as_is(string title, string hashTag)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.TwitterHandle = string.Empty;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = hashTag;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(podcast.Name);
        _mocker.GetMock<IPostModelFactory>()
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<Podcast, IEnumerable<Episode>> pe, bool _) =>
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
                            pe.Item2.First().ReleaseUtc.ToString("o"),
                            pe.Item2.First().Length.ToString(),
                            pe.Item2.First().Description,
                            pe.Item2.First().Id.ToString(),
                            pe.Item2.First().ReleaseUtc,
                            pe.Item2.First().Subjects.ToArray(),
                            pe.Item2.First().Urls.BBC,
                            pe.Item2.First().Urls.InternetArchive)
                    ],
                    null,
                    [],
                    []));
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), null);

        // Assert
        tweet.Should().Contain($"\"{title}\"");
        tweet.Should().NotContain("#");
    }

    [Fact(DisplayName =
        "TweetBuilder: an enrichment tag stored as Some-Tag is left out of the tweet, because writing #SomeTag drops the dash and that shortened tag is not the stored tag.")]
    public async Task enrichment_tag_that_shortens_the_stored_text_is_left_out()
    {
        // Arrange
        const string title = "Inside Some-Tag Today";
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = ["Some-Tag"];
            p.TwitterHandle = string.Empty;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = null;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(podcast.Name);
        _mocker.GetMock<IPostModelFactory>()
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<Podcast, IEnumerable<Episode>> pe, bool _) =>
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
                            pe.Item2.First().ReleaseUtc.ToString("o"),
                            pe.Item2.First().Length.ToString(),
                            pe.Item2.First().Description,
                            pe.Item2.First().Id.ToString(),
                            pe.Item2.First().ReleaseUtc,
                            pe.Item2.First().Subjects.ToArray(),
                            pe.Item2.First().Urls.BBC,
                            pe.Item2.First().Urls.InternetArchive)
                    ],
                    null,
                    [],
                    []));
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), null);

        // Assert
        tweet.Should().Contain($"\"{title}\"");
        tweet.Should().NotContain("#SomeTag");
    }
}
