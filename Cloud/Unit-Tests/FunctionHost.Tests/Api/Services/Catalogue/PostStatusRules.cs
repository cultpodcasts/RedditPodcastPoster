using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Films;
using FunctionHost.Tests.Api.Services.Films;
using Api.Services.News;
using Api.Services.TvShows;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.UrlShortening.Extensions;
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
    private SocialPostRemovalFingerprint? _tweetTarget;

    public PostStatusRules()
    {
        _mocker.GetMock<IFilmRepository>().ReplaceThroughSave();
        _mocker.Use(NullLogger<NewsReportService>.Instance);
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(new SearchCleanupMock().Cleanup);
        _mocker.Use<IPlayableSocialRetraction>(new PlayableSocialRetraction(
            _tweets.Object, _bluesky.Object, NullLogger<PlayableSocialRetraction>.Instance));
        _tweets.Setup(t => t.RemoveTweet(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(projection =>
            {
                _calls.Add("tweet");
                // The tweet manager matches with #1051's fingerprint of the projection it is given.
                _tweetTarget = SocialPostRemovalFingerprint.For(projection);
            })
            .ReturnsAsync(RemoveTweetState.Deleted);
        _bluesky.Setup(b => b.RemovePost(It.IsAny<PodcastEpisode>()))
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
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        report.SetNewsOrganisationProperties(organisation);
        report.SetRelease(CatalogueRelease.FromDate(DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(3))));
        customise?.Invoke(report);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync(report);
        return report;
    }

    private Task<NewsReportUpdateResult> Update(NewsReport report, NewsReportChangeRequest change) =>
        _mocker.CreateInstance<NewsReportService>()
            .UpdateAsync(new NewsReportChangeRequestWrapper(report.Id, change), CancellationToken.None);

    /// <summary>Post text laid out as #1051's SocialPostKindTemplate: quoted title, optional parent line, release line.</summary>
    private static string PostText(string title, string? parentLine, string releaseLine) =>
        parentLine == null
            ? $"\"{title}\"\n{releaseLine}\n#Synthetic\nhttps://t.co/synthetic"
            : $"\"{title}\"\n{parentLine}\n{releaseLine}\n#Synthetic\nhttps://t.co/synthetic";

    private static string DateLine(DateTime releaseUtc) =>
        releaseUtc.ToString(SocialPostKindTemplate.ReleaseFormat, CultureInfo.InvariantCulture);

    private static string LengthText(TimeSpan length) =>
        length.ToString(SocialPostKindTemplate.LengthFormat, CultureInfo.InvariantCulture);

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
        "PATCH news report tweeted=false on a tweeted report deletes the tweet laid out as the news post (organisation line, date, length), then clears the flag and saves, because the flag only drops once the tweet is gone.")]
    public async Task untweet_deletes_tweet_then_clears_and_saves()
    {
        // Arrange
        var report = CreateReport(r => r.Tweeted = true);
        var post = PostText(report.Title, report.NewsOrganisationName,
            $"{DateLine(report.ReleaseSort)} {LengthText(report.Length)}");

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Tweeted = false });

        // Assert
        _calls.Should().Equal("tweet", "save");
        _tweetTarget!.Id.Should().Be(report.Id);
        _tweetTarget.Matches(post).Should().BeTrue();
        _savedReport!.Tweeted.Should().BeFalse();
        result.Outcome!.TweetDeleted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "PATCH news report un-tweet matches the tweet on the release and length it was posted with, even when the same PATCH changes them, because the post text carries the old values.")]
    public async Task untweet_matches_pre_patch_release_and_length()
    {
        // Arrange
        var report = CreateReport(r => r.Tweeted = true);
        var post = PostText(report.Title, report.NewsOrganisationName,
            $"{DateLine(report.ReleaseSort)} {LengthText(report.Length)}");
        var newRelease = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(1));

        // Act
        await Update(report, new NewsReportChangeRequest
        {
            Tweeted = false,
            Release = newRelease.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Duration = (report.Length + TimeSpan.FromMinutes(1)).ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        });

        // Assert
        _tweetTarget!.Matches(post).Should().BeTrue();
        _savedReport!.Release!.Date.Should().Be(newRelease);
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
        _savedReport!.Tweeted.Should().BeFalse();
        result.Outcome!.TweetDeleted.Should().BeNull();
    }

    [Theory(DisplayName =
        "PATCH news report tweeted=false keeps tweeted=true and reports tweetDeleted=false when the tweet was not deleted, because a live tweet must never be forgotten and the curator can retry.")]
    [InlineData(RemoveTweetState.NotFound)]
    [InlineData(RemoveTweetState.TooManyRequests)]
    [InlineData(RemoveTweetState.Other)]
    public async Task untweet_not_deleted_keeps_flag(RemoveTweetState state)
    {
        // Arrange
        var report = CreateReport(r => r.Tweeted = true);
        _tweets.Setup(t => t.RemoveTweet(It.IsAny<PodcastEpisode>())).ReturnsAsync(state);

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Tweeted = false });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _savedReport!.Tweeted.Should().BeTrue();
        result.Outcome!.TweetDeleted.Should().BeFalse();
    }

    [Fact(DisplayName =
        "PATCH news report tweeted=false keeps tweeted=true when the tweet manager throws, and still saves the other changes, because the delete failed.")]
    public async Task untweet_exception_keeps_flag()
    {
        // Arrange
        var report = CreateReport(r => r.Tweeted = true);
        _tweets.Setup(t => t.RemoveTweet(It.IsAny<PodcastEpisode>())).ThrowsAsync(new InvalidOperationException("synthetic"));

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Tweeted = false, Posted = true });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _savedReport!.Tweeted.Should().BeTrue();
        _savedReport.Posted.Should().BeTrue();
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
        _bluesky.Verify(b => b.RemovePost(It.Is<PodcastEpisode>(x => x.Episode.BlueskyPost == SyntheticAtUri)), Times.Once);
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
        _bluesky.Setup(b => b.RemovePost(It.IsAny<PodcastEpisode>())).ReturnsAsync(state);

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
        "PATCH TV episode un-tweet and unBluesky find the post by the show line and a date line with no length when the length is unknown, and GET returns posted, because TV posts leave a zero length out.")]
    public async Task tv_episode_post_status()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Tweeted = true, BlueskyPost = SyntheticAtUri };
        episode.SetTvShowProperties(show);
        episode.SetRelease(CatalogueRelease.FromDate(DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(5))));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var post = PostText(episode.Title, show.Name, DateLine(episode.ReleaseSort));

        // Act
        var result = await _mocker.CreateInstance<TvShowEpisodeService>().UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(episode.Id,
                new TvShowEpisodeChangeRequest { Tweeted = false, UnBluesky = true, Posted = true }),
            CancellationToken.None);

        // Assert
        result.Outcome!.TweetDeleted.Should().BeTrue();
        result.Outcome.BlueskyPostDeleted.Should().BeTrue();
        _tweetTarget!.Matches(post).Should().BeTrue();
        _calls.Should().Equal("bluesky", "tweet");
        episode.ToDto().Posted.Should().BeTrue();
        episode.ToDto().Tweeted.Should().BeFalse();
        episode.ToDto().BlueskyPosted.Should().BeFalse();
    }

    [Fact(DisplayName =
        "PATCH film un-tweet finds the post by its expanded site link with no parent line, sets posted, and publisher is editable, because film posts have no parent line and their title is re-cased, so only the site link identifies them.")]
    public async Task film_post_status_and_publisher()
    {
        // Arrange
        var publisher = _fixture.CreateTitle();
        var film = new Film(_fixture.CreateTitle(3)) { Id = _fixture.CreateGuid(), Tweeted = true, BlueskyPost = SyntheticAtUri };
        film.SetRelease(CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 30));
        Film? saved = null;
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetFilm(film.Id)).ReturnsAsync(film);
        _mocker.GetMock<IFilmRepository>().Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(f => saved = f).Returns(Task.CompletedTask);
        var post = PostText(film.Name.ToUpperInvariant(), null,
            film.Release!.Year.ToString(CultureInfo.InvariantCulture));
        var siteLink = CataloguePlayableSiteLink.For(CatalogueParentKindMapper.ToPostProjection(film)).ToString();

        // Act
        var result = await _mocker.CreateInstance<FilmService>().UpdateAsync(
            new FilmChangeRequestWrapper(film.Id,
                new FilmChangeRequest { Tweeted = false, UnBluesky = true, Posted = true, PublisherName = $" {publisher} " }),
            CancellationToken.None);

        // Assert
        result.Outcome!.TweetDeleted.Should().BeTrue();
        result.Outcome.BlueskyPostDeleted.Should().BeTrue();
        // Twitter returns the link as t.co in the text, so the film is found by its expanded site link.
        _tweetTarget!.Matches(post, [siteLink]).Should().BeTrue();
        _tweetTarget.Matches(post).Should().BeFalse();
        saved!.Posted.Should().BeTrue();
        saved.Tweeted.Should().BeFalse();
        saved.BlueskyPosted.Should().BeFalse();
        saved.PublisherName.Should().Be(publisher);
        film.ToDto().PublisherName.Should().Be(publisher);
    }
}
