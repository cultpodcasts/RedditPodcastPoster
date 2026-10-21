using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Films;
using Api.Services.News;
using Api.Services.TvShows;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>Post status on news reports, TV episodes and films follows the podcast episode edit API.</summary>
public class PostStatusRules
{
    private const string SyntheticAtUri = "at://did:plc:synthetic/app.bsky.feed.post/3synthetic";
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<ITweetManager> _tweets = new();
    private readonly Mock<IBlueskyPostManager> _bluesky = new();
    private readonly List<string> _calls = [];
    private NewsReport? _savedReport;
    private string? _blueskyPostAtSave;

    public PostStatusRules()
    {
        _mocker.Use(NullLogger<NewsReportService>.Instance);
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(new SearchCleanupMock().Cleanup);
        _mocker.Use<IPlayableSocialRetraction>(new PlayableSocialRetraction(
            _tweets.Object, _bluesky.Object, NullLogger<PlayableSocialRetraction>.Instance));
        _tweets.Setup(t => t.RemoveTweet(It.IsAny<SocialPostRemovalTarget>()))
            .Callback(() => _calls.Add("tweet"))
            .ReturnsAsync(RemoveTweetState.Deleted);
        _bluesky.Setup(b => b.RemovePost(It.IsAny<SocialPostRemovalTarget>()))
            .Callback(() => _calls.Add("bluesky"))
            .ReturnsAsync(RemovePostState.Deleted);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(report =>
            {
                _calls.Add("save");
                _savedReport = report;
                _blueskyPostAtSave = report.BlueskyPost;
            })
            .Returns(Task.CompletedTask);
    }

    private NewsReport CreateReport(Action<NewsReport>? customise = null)
    {
        var organisation = new NewsOrganisation("Synthetic Evening News") { Id = _fixture.CreateGuid() };
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = new TimeSpan(0, 6, 30) };
        report.SetNewsOrganisationProperties(organisation);
        report.SetRelease(CatalogueRelease.FromDate(new DateOnly(2026, 10, 3)));
        customise?.Invoke(report);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync(report);
        return report;
    }

    private Task<NewsReportUpdateResult> Update(NewsReport report, NewsReportChangeRequest change) =>
        _mocker.CreateInstance<NewsReportService>()
            .UpdateAsync(new NewsReportChangeRequestWrapper(report.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH news report posted and tweeted=true set the flags with no social side effect, because the episode API only sets them.")]
    public async Task posted_and_tweeted_true_set_flags()
    {
        // Arrange
        var report = CreateReport();

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Posted = true, Tweeted = true });

        // Assert
        _savedReport!.Posted.Should().BeTrue();
        _savedReport.Tweeted.Should().BeTrue();
        result.Outcome!.TweetDeleted.Should().BeNull();
        result.Outcome.BlueskyPostDeleted.Should().BeNull();
        _tweets.VerifyNoOtherCalls();
        _bluesky.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "PATCH news report tweeted=false on a tweeted report clears the flag, saves, then deletes the tweet matched on the organisation name, length and release, because that is the episode un-tweet.")]
    public async Task untweet_deletes_tweet_after_save()
    {
        // Arrange
        var report = CreateReport(r => r.Tweeted = true);

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Tweeted = false });

        // Assert
        _savedReport!.Tweeted.Should().BeFalse();
        _calls.Should().Equal("save", "tweet");
        _tweets.Verify(t => t.RemoveTweet(It.Is<SocialPostRemovalTarget>(x =>
            x.Id == report.Id &&
            x.ParentName == "Synthetic Evening News" &&
            x.Length == new TimeSpan(0, 6, 30) &&
            x.ReleaseUtc == new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc))), Times.Once);
        result.Outcome!.TweetDeleted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "PATCH news report tweeted=false on an un-tweeted report deletes nothing, because the episode API only un-tweets on a true → false change.")]
    public async Task tweeted_false_when_not_tweeted_is_flag_only()
    {
        // Arrange
        var report = CreateReport();

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Tweeted = false });

        // Assert
        _tweets.VerifyNoOtherCalls();
        result.Outcome!.TweetDeleted.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report tweeted=false keeps the flag false even when the tweet delete fails, and reports tweetDeleted=false, because the episode API does the same.")]
    public async Task untweet_failure_still_clears_flag()
    {
        // Arrange
        var report = CreateReport(r => r.Tweeted = true);
        _tweets.Setup(t => t.RemoveTweet(It.IsAny<SocialPostRemovalTarget>())).ThrowsAsync(new InvalidOperationException("synthetic"));

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Tweeted = false });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _savedReport!.Tweeted.Should().BeFalse();
        result.Outcome!.TweetDeleted.Should().BeFalse();
    }

    [Fact(DisplayName =
        "PATCH news report unBluesky=true deletes the Bluesky post before saving and clears the Bluesky state, because the episode API deletes while the AT URI is still stored.")]
    public async Task unbluesky_deletes_then_clears()
    {
        // Arrange
        var report = CreateReport(r => r.BlueskyPost = SyntheticAtUri);

        // Act
        var result = await Update(report, new NewsReportChangeRequest { UnBluesky = true });

        // Assert
        _calls.Should().Equal("bluesky", "save");
        _bluesky.Verify(b => b.RemovePost(It.Is<SocialPostRemovalTarget>(x => x.BlueskyPost == SyntheticAtUri)), Times.Once);
        _blueskyPostAtSave.Should().BeNull();
        _savedReport!.BlueskyPosted.Should().BeFalse();
        result.Outcome!.BlueskyPostDeleted.Should().BeTrue();
    }

    [Theory(DisplayName =
        "PATCH news report unBluesky=true keeps the Bluesky state unless the post was deleted or already gone, because the episode API never forgets a live post.")]
    [InlineData(RemovePostState.NotFound, true)]
    [InlineData(RemovePostState.Other, false)]
    public async Task unbluesky_clears_only_when_gone(RemovePostState state, bool cleared)
    {
        // Arrange
        var report = CreateReport(r => r.BlueskyPost = SyntheticAtUri);
        _bluesky.Setup(b => b.RemovePost(It.IsAny<SocialPostRemovalTarget>())).ReturnsAsync(state);

        // Act
        var result = await Update(report, new NewsReportChangeRequest { UnBluesky = true });

        // Assert
        _savedReport!.BlueskyPosted.Should().Be(!cleared);
        result.Outcome!.BlueskyPostDeleted.Should().Be(cleared);
    }

    [Fact(DisplayName =
        "PATCH news report unBluesky=true on a report never posted to Bluesky does nothing, because the episode API only un-posts a posted item.")]
    public async Task unbluesky_when_not_posted_is_noop()
    {
        // Arrange
        var report = CreateReport();

        // Act
        var result = await Update(report, new NewsReportChangeRequest { UnBluesky = true });

        // Assert
        _bluesky.VerifyNoOtherCalls();
        result.Outcome!.BlueskyPostDeleted.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report with an invalid release changes no post status and calls no social network, because the request is validated first.")]
    public async Task invalid_request_touches_nothing()
    {
        // Arrange
        var report = CreateReport(r => { r.Tweeted = true; r.BlueskyPost = SyntheticAtUri; });

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Release = "bad", Tweeted = false, UnBluesky = true, Posted = true });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.BadRequest);
        report.Tweeted.Should().BeTrue();
        report.Posted.Should().BeFalse();
        _tweets.VerifyNoOtherCalls();
        _bluesky.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "PATCH TV episode un-tweet and unBluesky use the show name, and GET returns posted, because TV episodes share the playable post-status editor.")]
    public async Task tv_episode_post_status()
    {
        // Arrange
        var show = new TvShow("Synthetic Docuseries") { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Tweeted = true, BlueskyPost = SyntheticAtUri };
        episode.SetTvShowProperties(show);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _mocker.CreateInstance<TvShowEpisodeService>().UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(episode.Id,
                new TvShowEpisodeChangeRequest { Tweeted = false, UnBluesky = true, Posted = true }),
            CancellationToken.None);

        // Assert
        result.Outcome!.TweetDeleted.Should().BeTrue();
        result.Outcome.BlueskyPostDeleted.Should().BeTrue();
        _tweets.Verify(t => t.RemoveTweet(It.Is<SocialPostRemovalTarget>(x => x.ParentName == "Synthetic Docuseries")), Times.Once);
        episode.ToDto().Posted.Should().BeTrue();
        episode.ToDto().Tweeted.Should().BeFalse();
        episode.ToDto().BlueskyPosted.Should().BeFalse();
    }

    [Fact(DisplayName =
        "PATCH film un-tweet and unBluesky use the film name and set posted, and publisher is editable, because films carry the same post status and publisher fields.")]
    public async Task film_post_status_and_publisher()
    {
        // Arrange
        var film = new Film("Synthetic Cult Film") { Id = _fixture.CreateGuid(), Tweeted = true, BlueskyPost = SyntheticAtUri };
        Film? saved = null;
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetFilm(film.Id)).ReturnsAsync(film);
        _mocker.GetMock<IFilmRepository>().Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(f => saved = f).Returns(Task.CompletedTask);

        // Act
        var result = await _mocker.CreateInstance<FilmService>().UpdateAsync(
            new FilmChangeRequestWrapper(film.Id,
                new FilmChangeRequest { Tweeted = false, UnBluesky = true, Posted = true, PublisherName = " Synthetic Pictures " }),
            CancellationToken.None);

        // Assert
        result.Outcome!.TweetDeleted.Should().BeTrue();
        result.Outcome.BlueskyPostDeleted.Should().BeTrue();
        _tweets.Verify(t => t.RemoveTweet(It.Is<SocialPostRemovalTarget>(x => x.ParentName == "Synthetic Cult Film")), Times.Once);
        saved!.Posted.Should().BeTrue();
        saved.BlueskyPosted.Should().BeFalse();
        saved.PublisherName.Should().Be("Synthetic Pictures");
        film.ToDto().PublisherName.Should().Be("Synthetic Pictures");
    }
}
