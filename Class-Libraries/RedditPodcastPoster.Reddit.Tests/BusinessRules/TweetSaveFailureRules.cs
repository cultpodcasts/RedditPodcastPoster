using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>
/// #1205: when X accepted a post but the document cannot be saved as tweeted, the poster retries the save with
/// backoff, reports the live post as <see cref="TweetSendStatus.SentNotSaved"/> instead of throwing, and the hourly
/// run stops rather than posting the next candidate. Fakes only; no X calls.
/// </summary>
public class TweetSaveFailureRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly string _tweet;
    private readonly string _tweetId;

    public TweetSaveFailureRules()
    {
        _tweet = _fixture.Create<string>();
        _tweetId = _fixture.Create<long>().ToString();
        _mocker.Use(Options.Create(new TwitterOptions {MaxPosts = 5, MaxFailures = 5}));
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(It.IsAny<PodcastEpisode>(), It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(() => _tweet);
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PostTweetResponse(TweetSendStatus.Sent, TweetId: _tweetId));
    }

    private PodcastEpisode CreateCandidate()
    {
        var podcast = _fixture.CreatePodcast();
        return new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
    }

    private PodcastEpisode CreateCatalogueCandidate()
    {
        var podcast = _fixture.CreatePodcast();
        return new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast))
        {
            ContentKind = nameof(ContentKind.Film)
        };
    }

    [Fact(DisplayName =
        "When X accepts a post and the save fails once, then the save is retried and the episode ends saved as tweeted with the new tweet id, because a transient persistence blip must not leave a live post unrecorded.")]
    public async Task transient_save_failure_is_retried_until_it_succeeds()
    {
        // Arrange
        var item = CreateCandidate();
        _mocker.GetMock<IEpisodeRepository>()
            .SetupSequence(x => x.Save(It.IsAny<Episode>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()))
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        var result = await sut.PostTweet(item, null);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.Sent);
        item.Episode.Tweeted.Should().BeTrue();
        item.Episode.TweetPost.Should().Be(_tweetId);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Exactly(2));
        _mocker.GetMock<ITweetSaveBackoff>().Verify(x => x.Delay(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When X accepts a post and every save attempt fails, then the poster reports SentNotSaved with the tweet id instead of throwing, because the live post must be surfaced so the run can stop.")]
    public async Task persistent_save_failure_reports_sent_not_saved()
    {
        // Arrange
        var item = CreateCandidate();
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(x => x.Save(It.IsAny<Episode>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        var result = await sut.PostTweet(item, null);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.SentNotSaved);
        result.TweetId.Should().Be(_tweetId);
        item.Episode.Tweeted.Should().BeTrue();
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.IsAny<Episode>()), Times.Exactly(TweetPoster.MaxSaveAttempts));
        _mocker.GetMock<ITweetSaveBackoff>()
            .Verify(x => x.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Exactly(TweetPoster.MaxSaveAttempts - 1));
    }

    [Fact(DisplayName =
        "When X accepts a catalogue item's post and every MarkTweeted attempt fails, then the poster reports SentNotSaved, because catalogue kinds persist through the same retry and stop signal as episodes.")]
    public async Task persistent_catalogue_save_failure_reports_sent_not_saved()
    {
        // Arrange
        var item = CreateCatalogueCandidate();
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Setup(x => x.MarkTweeted(It.IsAny<PodcastEpisode>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        var result = await sut.PostTweet(item, null);

        // Assert
        result.TweetSendStatus.Should().Be(TweetSendStatus.SentNotSaved);
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Verify(x => x.MarkTweeted(item, _tweetId), Times.Exactly(TweetPoster.MaxSaveAttempts));
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "When X refuses a duplicate and the save fails after retries, then the failure still surfaces as an exception, because a duplicate has no live post to protect and the next run can record it again.")]
    public async Task duplicate_save_failure_still_throws()
    {
        // Arrange
        var item = CreateCandidate();
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.DuplicateForbidden, _tweet));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(x => x.Save(It.IsAny<Episode>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        var post = () => sut.PostTweet(item, null);

        // Assert
        await post.Should().ThrowAsync<InvalidOperationException>();
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.IsAny<Episode>()), Times.Exactly(TweetPoster.MaxSaveAttempts));
    }

    [Fact(DisplayName =
        "When a post is accepted by X but not saved, then the run counts it and tries no later podcast episode or catalogue item, because posting the next candidate while persistence is failing risks a double post.")]
    public async Task sent_not_saved_stops_the_run()
    {
        // Arrange
        var first = CreateCandidate();
        var second = CreateCandidate();
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetUntweetedPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync([first, second]);
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(It.IsAny<PodcastEpisode>(), It.IsAny<bool>()))
            .ReturnsAsync(new Cloudflare.Models.WriteResult(true, _fixture.Create<Uri>()));
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(first, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.SentNotSaved, _tweet, _tweetId));
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(second, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        _mocker.GetMock<ITweetPoster>().Verify(x => x.PostTweet(first, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Once);
        _mocker.GetMock<ITweetPoster>().Verify(x => x.PostTweet(second, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Never);
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }
}
