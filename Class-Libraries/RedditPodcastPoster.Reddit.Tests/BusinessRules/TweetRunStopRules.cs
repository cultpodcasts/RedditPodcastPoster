using System.Net;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>
/// The hourly tweet run stops at the first refusal that applies to every candidate (429, 402, 401/403), so a run
/// with depleted X credits sends one request instead of one per candidate. Fakes only; no X calls.
/// </summary>
public class TweetRunStopRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private (PodcastEpisode First, PodcastEpisode Second) ArrangeTwoCandidates(TweetSendStatus firstStatus)
    {
        var podcast = _fixture.CreatePodcast();
        var first = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        var second = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetUntweetedPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync([first, second]);
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(It.IsAny<PodcastEpisode>(), It.IsAny<bool>()))
            .ReturnsAsync(new Cloudflare.Models.WriteResult(true, new Uri("https://s.example.test/abc")));
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(first, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(firstStatus, "tweet"));
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(second, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        return (first, second);
    }

    [Theory(DisplayName =
        "When X refuses the first candidate for a run-wide reason, then no later candidate or catalogue item is tried, because every send would be refused the same way.")]
    [InlineData(TweetSendStatus.PaymentRequired)]
    [InlineData(TweetSendStatus.TooManyRequests)]
    [InlineData(TweetSendStatus.AuthenticationFailed)]
    public async Task run_wide_refusal_stops_the_run(TweetSendStatus status)
    {
        // Arrange
        var (first, second) = ArrangeTwoCandidates(status);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        _mocker.GetMock<ITweetPoster>().Verify(x => x.PostTweet(first, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Once);
        _mocker.GetMock<ITweetPoster>().Verify(x => x.PostTweet(second, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Never);
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }

    [Fact(DisplayName =
        "When the first candidate fails for a post-specific reason, then the next candidate is tried, because other posts can still succeed.")]
    public async Task post_specific_failure_tries_the_next_candidate()
    {
        // Arrange
        var (_, second) = ArrangeTwoCandidates(TweetSendStatus.Failed);
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        _mocker.GetMock<ITweetPoster>().Verify(x => x.PostTweet(second, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Once);
    }

    [Theory(DisplayName =
        "When X refuses a post with 402, 401, 403 or another error, then the episode is not saved as tweeted, because it must be retried on the next run.")]
    [InlineData(TweetSendStatus.PaymentRequired)]
    [InlineData(TweetSendStatus.AuthenticationFailed)]
    [InlineData(TweetSendStatus.TooManyRequests)]
    [InlineData(TweetSendStatus.Failed)]
    public async Task refused_post_keeps_tweeted_false(TweetSendStatus status)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var item = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        item.Episode.Tweeted = false;
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync("tweet");
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.Send("tweet"))
            .ReturnsAsync(new PostTweetResponse(status, "tweet"));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        var result = await sut.PostTweet(item, null);

        // Assert
        result.TweetSendStatus.Should().Be(status);
        item.Episode.Tweeted.Should().BeFalse();
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a tweet is sent, then the episode is saved with Tweeted=true, because a successful post must not repeat.")]
    public async Task sent_post_marks_tweeted()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var item = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        item.Episode.Tweeted = false;
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync("tweet");
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.Send("tweet"))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        item.Episode.Tweeted.Should().BeTrue();
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted)), Times.Once);
    }

    [Theory(DisplayName =
        "When a post fails with a given status code, then it is classified so the run can tell account-wide failures from one-post failures.")]
    [InlineData(HttpStatusCode.PaymentRequired, TweetSendStatus.PaymentRequired)]
    [InlineData(HttpStatusCode.Unauthorized, TweetSendStatus.AuthenticationFailed)]
    [InlineData(HttpStatusCode.Forbidden, TweetSendStatus.AuthenticationFailed)]
    [InlineData(HttpStatusCode.BadRequest, TweetSendStatus.Failed)]
    [InlineData(HttpStatusCode.InternalServerError, TweetSendStatus.Failed)]
    public void failure_status_codes_are_classified(HttpStatusCode code, TweetSendStatus expected)
    {
        TweetSendFailureClassifier.Classify(code).Should().Be(expected);
    }
}
