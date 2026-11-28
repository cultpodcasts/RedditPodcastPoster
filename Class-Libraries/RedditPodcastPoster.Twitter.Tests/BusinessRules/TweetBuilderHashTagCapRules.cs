using System.Text.RegularExpressions;
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

public class TweetBuilderHashTagCapRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TweetBuilderHashTagCapRules()
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

    [Fact(DisplayName =
        "TweetBuilder: when more than two end-line hashtags are available and no brand TwitterOptions.HashTag is set, then BuildTweet keeps at most two in source order, because X Help recommends at most two hashtags per post (MaxHashTags).")]
    public async Task build_tweet_keeps_at_most_two_end_hashtags_without_brand()
    {
        // Arrange
        const string episodeHashTag = "#Alpha #Bravo #Charlie";
        // Title must not contain Alpha/Bravo/Charlie or AddHashTag would move them into the title.
        const string title = "Synthetic briefing notes";
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
            e.HashTag = episodeHashTag;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        ArrangeSanitiserAndPostModel(podcast, title);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), null);

        // Assert
        CountHashTags(tweet).Should().Be(TweetBuilder.MaxHashTags);
        tweet.Should().Contain("#Alpha");
        tweet.Should().Contain("#Bravo");
        tweet.Should().NotContain("#Charlie");
    }

    [Fact(DisplayName =
        "TweetBuilder: when TwitterOptions.HashTag (brand, e.g. Cult) is set and surplus episode end-line tags exist, then BuildTweet keeps the brand tag plus at most one other end-line tag, because the brand hashtag reserves one of the two MaxHashTags slots and X Help recommends at most two hashtags per post.")]
    public async Task build_tweet_prefers_brand_hashtag_within_max_budget()
    {
        // Arrange — HashTag literal is the production option under test (#Cult / #1207).
        const string globalOptionHashTag = "Cult";
        const string episodeHashTag = "#Alpha #Bravo #Charlie";
        // Title must not contain Alpha/Bravo/Charlie/Cult or AddHashTag would move them into the title.
        const string title = "Synthetic briefing notes";
        _mocker.Use(Options.Create(new TwitterOptions
        {
            ConsumerKey = "k",
            ConsumerSecret = "s",
            AccessToken = "t",
            AccessTokenSecret = "ts",
            HashTag = globalOptionHashTag,
            WithEpisodeUrl = false
        }));
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
            e.HashTag = episodeHashTag;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        ArrangeSanitiserAndPostModel(podcast, title);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), null);

        // Assert
        CountHashTags(tweet).Should().Be(TweetBuilder.MaxHashTags);
        tweet.Should().Contain($"#{globalOptionHashTag}");
        tweet.Should().Contain("#Alpha");
        tweet.Should().NotContain("#Bravo");
        tweet.Should().NotContain("#Charlie");
    }

    [Fact(DisplayName =
        "TweetBuilder: when the title already carries one enrichment hashtag and TwitterOptions.HashTag is set, then BuildTweet adds the brand end-line tag and omits further end-line tags, because the brand reserves one MaxHashTags slot and the enrichment used the other.")]
    public async Task build_tweet_title_enrichment_plus_brand_fills_budget()
    {
        // Arrange
        const string enrichmentTag = "Watch";
        const string globalOptionHashTag = "Cult";
        const string endTag = "Extra";
        _mocker.Use(Options.Create(new TwitterOptions
        {
            ConsumerKey = "k",
            ConsumerSecret = "s",
            AccessToken = "t",
            AccessTokenSecret = "ts",
            HashTag = globalOptionHashTag,
            WithEpisodeUrl = false
        }));
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.TwitterHandle = string.Empty;
        });
        var title = $"Synthetic {enrichmentTag} briefing";
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = $"#{endTag} #Spare";
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync([new HashTag(enrichmentTag, enrichmentTag)]);
        ArrangeSanitiserAndPostModel(podcast, title);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), null);

        // Assert
        CountHashTags(tweet).Should().Be(TweetBuilder.MaxHashTags);
        tweet.Should().Contain($"#{enrichmentTag}");
        tweet.Should().Contain($"#{globalOptionHashTag}");
        tweet.Should().NotContain($"#{endTag}");
        tweet.Should().NotContain("#Spare");
    }

    [Fact(DisplayName =
        "TweetBuilder: when the title already carries two enrichment hashtags and no brand TwitterOptions.HashTag is set, then BuildTweet adds no end-line hashtags, because the two-hashtag MaxHashTags budget is already used.")]
    public async Task build_tweet_skips_end_hashtags_when_title_used_the_budget()
    {
        // Arrange
        const string firstTag = "Watch";
        const string secondTag = "Other";
        const string endTag = "Extra";
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.TwitterHandle = string.Empty;
        });
        var title = $"Synthetic {firstTag} {secondTag} briefing";
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = $"#{endTag} #Spare";
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(
            [
                new HashTag(firstTag, firstTag),
                new HashTag(secondTag, secondTag)
            ]);
        ArrangeSanitiserAndPostModel(podcast, title);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), null);

        // Assert
        CountHashTags(tweet).Should().Be(TweetBuilder.MaxHashTags);
        tweet.Should().Contain($"#{firstTag}");
        tweet.Should().Contain($"#{secondTag}");
        tweet.Should().NotContain($"#{endTag}");
        tweet.Should().NotContain("#Spare");
    }

    private void ArrangeSanitiserAndPostModel(Podcast podcast, string title)
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

    private static int CountHashTags(string tweet) =>
        Regex.Matches(tweet, @"#\w+").Count;
}
