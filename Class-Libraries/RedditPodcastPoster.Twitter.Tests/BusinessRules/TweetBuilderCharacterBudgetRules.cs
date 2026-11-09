using System.Text;
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
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Tests.BusinessRules;

public class TweetBuilderCharacterBudgetRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TweetBuilderCharacterBudgetRules()
    {
        _mocker.Use<IHashTagEnricher>(new HashTagEnricher());
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact(DisplayName =
        "The tweet body budget before the primary URL is XTweetCharacterLimit minus one t.co weight, because X counts each link as a 23-character t.co URL.")]
    public void character_budget_before_primary_url_is_limit_minus_one_tco()
    {
        // Arrange
        // Act
        var budget = TweetBuilder.CharacterBudgetBeforePrimaryUrl;
        var tco = TweetBuilder.TcoUrlCharacterWeight;

        // Assert
        TweetBuilder.XTweetCharacterLimit.Should().Be(280);
        tco.Should().Be(23);
        budget.Should().Be(TweetBuilder.XTweetCharacterLimit - tco);
        budget.Should().Be(257);
    }

    [Fact(DisplayName =
        "When a second short URL will be appended, the title reserve is 26 (one t.co plus separator allowance), so the composed tweet stays within 280.")]
    public void second_url_reserve_is_tco_plus_separator_allowance()
    {
        // Arrange
        // Act
        var reserve = TweetBuilder.SecondUrlReserve;

        // Assert
        reserve.Should().Be(TweetBuilder.TcoUrlCharacterWeight + 3);
        reserve.Should().Be(26);
    }

    [Fact(DisplayName =
        "BuildTweet truncates an over-long title to CharacterBudgetBeforePrimaryUrl minus the header overhead, so the body still leaves room for one primary t.co URL.")]
    public async Task build_tweet_truncates_title_using_character_budget_before_primary_url()
    {
        // Arrange
        const string podcastName = "Budget Show";
        var longTitle = new string('A', TweetBuilder.CharacterBudgetBeforePrimaryUrl);
        ConfigureTwitterOptions(withEpisodeUrl: false);
        var (podcast, episode) = CreateEpisodeWithYouTubeLink(podcastName, longTitle);
        ConfigureTweetBuilderMocks(podcast, longTitle);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var headerLength = MeasureHeaderLengthWithoutTitle(podcastEpisode, podcastName);
        var expectedPermittedTitleLength =
            TweetBuilder.CharacterBudgetBeforePrimaryUrl - headerLength;
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(podcastEpisode, shortUrl: null);

        // Assert
        var quotedTitle = ExtractQuotedTitle(tweet);
        quotedTitle.Should().EndWith("…");
        quotedTitle.Length.Should().Be(expectedPermittedTitleLength);
        expectedPermittedTitleLength.Should().BeLessThan(TweetBuilder.CharacterBudgetBeforePrimaryUrl);
    }

    [Fact(DisplayName =
        "BuildTweet reserves SecondUrlReserve from the title budget when a short site URL will follow the primary link, because two t.co URLs must still fit in 280.")]
    public async Task build_tweet_applies_second_url_reserve_when_short_url_will_be_appended()
    {
        // Arrange
        const string podcastName = "Reserve Show";
        var longTitle = new string('B', TweetBuilder.CharacterBudgetBeforePrimaryUrl);
        ConfigureTwitterOptions(withEpisodeUrl: true);
        var (podcast, episode) = CreateEpisodeWithYouTubeLink(podcastName, longTitle);
        // Subjects force AlwaysLinksSite / reserveSecondUrl path with WithEpisodeUrl.
        episode.Subjects = ["budget-subject"];
        ConfigureTweetBuilderMocks(podcast, longTitle);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var shortUrl = new Uri("https://cultpodcasts.com/e/budget");
        var headerLength = MeasureHeaderLengthWithoutTitle(podcastEpisode, podcastName);
        var expectedPermittedTitleLength =
            TweetBuilder.CharacterBudgetBeforePrimaryUrl - (headerLength + TweetBuilder.SecondUrlReserve);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(podcastEpisode, shortUrl);

        // Assert
        var quotedTitle = ExtractQuotedTitle(tweet);
        quotedTitle.Should().EndWith("…");
        quotedTitle.Length.Should().Be(expectedPermittedTitleLength);
        tweet.Should().Contain(shortUrl.AbsoluteUri);
        expectedPermittedTitleLength.Should().BeLessThan(
            TweetBuilder.CharacterBudgetBeforePrimaryUrl - headerLength);
    }

    private void ConfigureTwitterOptions(bool withEpisodeUrl)
    {
        _mocker.Use(Options.Create(new TwitterOptions
        {
            ConsumerKey = "k",
            ConsumerSecret = "s",
            AccessToken = "t",
            AccessTokenSecret = "ts",
            HashTag = null,
            WithEpisodeUrl = withEpisodeUrl
        }));
    }

    private (Podcast Podcast, Episode Episode) CreateEpisodeWithYouTubeLink(string podcastName, string title)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = podcastName;
            p.HashTag = null;
            p.EnrichmentHashTags = null;
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
        return (podcast, episode);
    }

    private void ConfigureTweetBuilderMocks(Podcast podcast, string title)
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

    /// <summary>
    /// Mirrors the header BuildTweet appends before computing the title budget (parent line + release).
    /// </summary>
    private static int MeasureHeaderLengthWithoutTitle(PodcastEpisode podcastEpisode, string podcastName)
    {
        var header = new StringBuilder();
        if (SocialPostKindTemplate.HasParentLine(podcastEpisode))
        {
            header.AppendLine(podcastName);
        }

        header.AppendLine(SocialPostKindTemplate.ReleaseLine(podcastEpisode));
        return header.Length;
    }

    private static string ExtractQuotedTitle(string tweet)
    {
        var firstLine = tweet.Split(["\r\n", "\n"], StringSplitOptions.None)[0];
        firstLine.Should().StartWith("\"").And.EndWith("\"");
        return firstLine[1..^1];
    }
}
