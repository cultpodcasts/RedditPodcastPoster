using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Twitter.Tests.BusinessRules;

/// <summary>
/// The hourly tweet run honours twitter__MaxPosts and twitter__MaxFailures the same way Bluesky
/// honours its caps: podcast episodes first, catalogue items filling leftover slots, and a
/// run-wide X refusal stopping immediately.
/// </summary>
public class TweeterMaxPostsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TweeterMaxPostsRules()
    {
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(It.IsAny<PodcastEpisode>(), It.IsAny<bool>()))
            .ReturnsAsync(() => new WriteResult(true, _fixture.Create<Uri>()));
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Setup(x => x.GetUntweeted())
            .ReturnsAsync(Array.Empty<PodcastEpisode>());
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetUntweetedPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync(Array.Empty<PodcastEpisode>());
    }

    private void UseOptions(int maxPosts = 1, int maxFailures = 5) =>
        _mocker.Use(Options.Create(new TwitterOptions
        {
            MaxPosts = maxPosts,
            MaxFailures = maxFailures
        }));

    private PodcastEpisode CreatePodcastEpisode()
    {
        var podcast = _fixture.CreatePodcast();
        return new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
    }

    private PodcastEpisode CreateCatalogueEpisode()
    {
        var podcast = _fixture.CreatePodcast();
        return new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast))
        {
            ContentKind = nameof(ContentKind.Film)
        };
    }

    private void SetupPodcasts(params PodcastEpisode[] items)
    {
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetUntweetedPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync(items);
    }

    private void SetupCatalogue(params PodcastEpisode[] items)
    {
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Setup(x => x.GetUntweeted())
            .ReturnsAsync(items);
    }

    private void PosterReturns(PodcastEpisode item, TweetSendStatus status)
    {
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(status));
    }

    private void VerifyPosted(PodcastEpisode item, Times times) =>
        _mocker.GetMock<ITweetPoster>()
            .Verify(x => x.PostTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()), times);

    [Fact(DisplayName =
        "When MaxPosts is left at its default of 1 and the first podcast episode is tweeted, then no later podcast episode or catalogue item is tried, because the historical one-tweet-per-run cadence is unchanged.")]
    public async Task default_max_posts_still_sends_one_tweet()
    {
        // Arrange
        UseOptions();
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        var catalogueItem = CreateCatalogueEpisode();
        SetupPodcasts(first, second);
        SetupCatalogue(catalogueItem);
        PosterReturns(first, TweetSendStatus.Sent);
        PosterReturns(second, TweetSendStatus.Sent);
        PosterReturns(catalogueItem, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Never());
        VerifyPosted(catalogueItem, Times.Never());
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }

    [Fact(DisplayName =
        "When MaxPosts is N and more than N podcast episodes are waiting, then only N tweets are sent and catalogue items are not looked up, because podcast episodes use the whole budget first.")]
    public async Task max_posts_caps_the_run_at_n()
    {
        // Arrange
        const int maxPosts = 3;
        UseOptions(maxPosts: maxPosts);
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        var third = CreatePodcastEpisode();
        var fourth = CreatePodcastEpisode();
        SetupPodcasts(first, second, third, fourth);
        PosterReturns(first, TweetSendStatus.Sent);
        PosterReturns(second, TweetSendStatus.Sent);
        PosterReturns(third, TweetSendStatus.Sent);
        PosterReturns(fourth, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Once());
        VerifyPosted(third, Times.Once());
        VerifyPosted(fourth, Times.Never());
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }

    [Fact(DisplayName =
        "When MaxFailures is reached by post-specific failures, then no later candidate or catalogue item is tried, because the failure budget is exhausted.")]
    public async Task failure_cap_stops_the_run()
    {
        // Arrange
        UseOptions(maxPosts: 5, maxFailures: 2);
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        var third = CreatePodcastEpisode();
        SetupPodcasts(first, second, third);
        PosterReturns(first, TweetSendStatus.Failed);
        PosterReturns(second, TweetSendStatus.Failed);
        PosterReturns(third, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Once());
        VerifyPosted(third, Times.Never());
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }

    [Fact(DisplayName =
        "When a post-specific failure is below MaxFailures, then the next candidate is still tried, because other posts can still succeed.")]
    public async Task failure_below_the_cap_tries_the_next_candidate()
    {
        // Arrange
        UseOptions(maxPosts: 2, maxFailures: 2);
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        SetupPodcasts(first, second);
        PosterReturns(first, TweetSendStatus.Failed);
        PosterReturns(second, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Once());
    }

    [Theory(DisplayName =
        "When MaxPosts still has room and X refuses a candidate for a run-wide reason (or a live post could not be saved), then no later candidate or catalogue item is tried, because every send would be refused the same way or persistence is failing.")]
    [InlineData(TweetSendStatus.PaymentRequired)]
    [InlineData(TweetSendStatus.TooManyRequests)]
    [InlineData(TweetSendStatus.AuthenticationFailed)]
    [InlineData(TweetSendStatus.ServerError)]
    [InlineData(TweetSendStatus.Timeout)]
    [InlineData(TweetSendStatus.SentNotSaved)]
    public async Task stop_run_ends_the_run_even_when_max_posts_has_room(TweetSendStatus status)
    {
        // Arrange
        UseOptions(maxPosts: 5, maxFailures: 5);
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        SetupPodcasts(first, second);
        PosterReturns(first, status);
        PosterReturns(second, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Never());
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }

    [Fact(DisplayName =
        "When MaxPosts has room after podcast episodes are tweeted, then catalogue items fill the remaining slots and later catalogue items are not tried, because they share the run's budget.")]
    public async Task catalogue_items_fill_remaining_slots()
    {
        // Arrange
        UseOptions(maxPosts: 2);
        var podcastItem = CreatePodcastEpisode();
        var firstCatalogue = CreateCatalogueEpisode();
        var secondCatalogue = CreateCatalogueEpisode();
        SetupPodcasts(podcastItem);
        SetupCatalogue(firstCatalogue, secondCatalogue);
        PosterReturns(podcastItem, TweetSendStatus.Sent);
        PosterReturns(firstCatalogue, TweetSendStatus.Sent);
        PosterReturns(secondCatalogue, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(podcastItem, Times.Once());
        VerifyPosted(firstCatalogue, Times.Once());
        VerifyPosted(secondCatalogue, Times.Never());
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Once);
    }

    [Fact(DisplayName =
        "When posting a candidate throws, then that throw counts toward MaxFailures and the run stops once the cap is reached, because a broken send is an unsuccessful post.")]
    public async Task thrown_send_counts_toward_the_failure_cap()
    {
        // Arrange
        UseOptions(maxPosts: 5, maxFailures: 1);
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        SetupPodcasts(first, second);
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(first, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));
        PosterReturns(second, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Never());
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }

    [Theory(DisplayName =
        "When MaxPosts and MaxFailures are both 1 and the first candidate is already resolved as Untweetable or DuplicateForbidden, then the next candidate is still tweeted and a later one is not, because a skip spends neither budget.")]
    [InlineData(TweetSendStatus.Untweetable)]
    [InlineData(TweetSendStatus.DuplicateForbidden)]
    public async Task resolved_skips_do_not_count_as_failures(TweetSendStatus status)
    {
        // Arrange
        UseOptions(maxPosts: 1, maxFailures: 1);
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        var third = CreatePodcastEpisode();
        SetupPodcasts(first, second, third);
        PosterReturns(first, status);
        PosterReturns(second, TweetSendStatus.Sent);
        PosterReturns(third, TweetSendStatus.Sent);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Once());
        VerifyPosted(third, Times.Never());
    }
}
