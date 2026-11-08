using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>
/// Permanent BuildTweet failures must record a skip reason and stop hourly shortener retries (#1209).
/// </summary>
public class UntweetableSkipRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "When BuildTweet fails because the title budget is exhausted, then the podcast episode is saved with tweetSkipReason and Tweeted=true, because the hourly run must not rewrite the shortener every hour.")]
    public async Task title_too_long_records_skip_reason_on_podcast_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var item = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        item.Episode.OldTweeted = false;
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ThrowsAsync(new UntweetableException(TweetSkipReasons.TitleTooLong, "synthetic title budget"));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        var result = await sut.PostTweet(item, null);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.Untweetable);
        item.Episode.TweetSkipReason.Should().Be(TweetSkipReasons.TitleTooLong);
        item.Episode.Tweeted.Should().BeTrue();
        item.Episode.OldTweeted.Should().BeFalse();
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e =>
                e.TweetSkipReason == TweetSkipReasons.TitleTooLong &&
                e.Tweeted &&
                e.OldTweeted != true)), Times.Once);
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Verify(x => x.MarkNotTweetable(It.IsAny<PodcastEpisode>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "When BuildTweet fails because there is no link, then the catalogue writer records the skip reason and no podcast episode is saved, because a projection must never land in the episodes container.")]
    public async Task no_link_records_skip_reason_on_catalogue_playable()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var item = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast))
        {
            ContentKind = nameof(ContentKind.Film)
        };
        item.Episode.OldTweeted = false;
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ThrowsAsync(new UntweetableException(TweetSkipReasons.NoLink, "synthetic no link"));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        var result = await sut.PostTweet(item, CataloguePlayableSiteLink.For(item));

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.Untweetable);
        item.Episode.TweetSkipReason.Should().Be(TweetSkipReasons.NoLink);
        item.Episode.Tweeted.Should().BeTrue();
        item.Episode.OldTweeted.Should().BeFalse();
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Verify(x => x.MarkNotTweetable(item, TweetSkipReasons.NoLink), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the first candidate is permanently untweetable, then the next candidate is still tried and the shortener is not written again for later candidates in this path, because a form failure is post-specific.")]
    public async Task untweetable_candidate_tries_the_next()
    {
        // Arrange
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
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Untweetable));
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(second, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        _mocker.GetMock<ITweetPoster>().Verify(x => x.PostTweet(first, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Once);
        _mocker.GetMock<ITweetPoster>().Verify(x => x.PostTweet(second, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Once);
        _mocker.GetMock<IShortnerService>().Verify(x => x.Write(first, It.IsAny<bool>()), Times.Once);
        _mocker.GetMock<IShortnerService>().Verify(x => x.Write(second, It.IsAny<bool>()), Times.Once);
    }
}
