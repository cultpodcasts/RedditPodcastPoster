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

public class TweetBuilderAtMentionRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private TwitterOptions _twitterOptions = null!;

    public TweetBuilderAtMentionRules()
    {
        _twitterOptions = new TwitterOptions
        {
            ConsumerKey = "k",
            ConsumerSecret = "s",
            AccessToken = "t",
            AccessTokenSecret = "ts",
            HashTag = null,
            WithEpisodeUrl = false,
            IncludeAtMentions = false
        };
        _mocker.Use<IHashTagEnricher>(new HashTagEnricher());
        _mocker.Use(Options.Create(_twitterOptions));
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());
    }

    [Fact(DisplayName =
        "TweetBuilder: when IncludeAtMentions is false (default), then the tweet has the podcast name but no podcast or guest @handles, because X rejects unsolicited programmatic mentions on self-serve tiers (#1202).")]
    public async Task build_tweet_omits_at_handles_when_include_at_mentions_is_off()
    {
        // Arrange — handles would be appended if IncludeAtMentions were on; assert they stay out.
        const string podcastHandle = "@PodHandleOff";
        const string guestHandle = "@GuestHandleOff";
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.TwitterHandle = podcastHandle;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Subjects = [];
            e.HashTag = null;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        ArrangeSanitisers(podcast, episode.Title);
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((new[] { guestHandle }, Array.Empty<string>()));
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), shortUrl: null);

        // Assert
        tweet.Should().Contain(podcast.Name);
        tweet.Should().NotContain(podcastHandle);
        tweet.Should().NotContain(guestHandle);
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Verify(x => x.Resolve(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "TweetBuilder: when IncludeAtMentions is true, then the tweet appends the podcast and guest @handles, because operators may opt in to programmatic mentions.")]
    public async Task build_tweet_appends_at_handles_when_include_at_mentions_is_on()
    {
        // Arrange
        _twitterOptions.IncludeAtMentions = true;
        const string podcastHandle = "@PodHandleOn";
        const string guestHandle = "@GuestHandleOn";
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.TwitterHandle = podcastHandle;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Subjects = [];
            e.HashTag = null;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        ArrangeSanitisers(podcast, episode.Title);
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((new[] { guestHandle }, Array.Empty<string>()));
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), shortUrl: null);

        // Assert
        tweet.Should().Contain(podcastHandle);
        tweet.Should().Contain(guestHandle);
    }

    private void ArrangeSanitisers(Podcast podcast, string title)
    {
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
    }
}
