using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Bluesky.Tests.BusinessRules;

/// <summary>
/// Bluesky's platform-owned failure-budget mapping over <c>SocialPostRunBudget</c>:
/// EpisodeNotFound and unexpected exceptions continue without RecordFailure (#1455),
/// unlike Twitter's throw-as-failure mapping.
/// </summary>
public class BlueskyPostManagerFailureBudgetRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public BlueskyPostManagerFailureBudgetRules()
    {
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(It.IsAny<PodcastEpisode>(), It.IsAny<bool>()))
            .ReturnsAsync(() => new WriteResult(true, _fixture.Create<Uri>()));
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Setup(x => x.GetBlueskyReady())
            .ReturnsAsync(Array.Empty<PodcastEpisode>());
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetBlueskyReadyPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync(Array.Empty<PodcastEpisode>());
    }

    private void UseOptions(int maxPosts = 5, int maxFailures = 1) =>
        _mocker.Use(Options.Create(new BlueskyOptions
        {
            Identifier = _fixture.Create<string>(),
            Password = _fixture.Create<string>(),
            WithEpisodeUrl = true,
            ShortUrlOnlyWhenShareImage = false,
            ReuseSession = false,
            MaxPosts = maxPosts,
            MaxFailures = maxFailures
        }));

    private PodcastEpisode CreatePodcastEpisode()
    {
        var podcast = _fixture.CreatePodcast();
        return new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
    }

    private void SetupPodcasts(params PodcastEpisode[] items)
    {
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetBlueskyReadyPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync(items);
    }

    private void PosterReturns(PodcastEpisode item, BlueskySendStatus status)
    {
        _mocker.GetMock<IBlueskyPoster>()
            .Setup(x => x.Post(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(status);
    }

    private void VerifyPosted(PodcastEpisode item, Times times) =>
        _mocker.GetMock<IBlueskyPoster>()
            .Verify(x => x.Post(item, It.IsAny<Uri?>(), It.IsAny<bool>()), times);

    public static TheoryData<Exception> ThrowsThatDoNotSpendFailureBudget()
    {
        var fixture = new DomainTestFixture();
        return new TheoryData<Exception>
        {
            new EpisodeNotFoundException(fixture.CreateSpotifyId(), Service.Spotify),
            new InvalidOperationException(fixture.Create<string>())
        };
    }

    [Theory(DisplayName =
        "When posting a candidate throws EpisodeNotFoundException or an unexpected exception, then that throw does not count toward MaxFailures and the next candidate is still posted, because Bluesky continues without spending the failure budget (unlike Twitter's throw-as-failure mapping).")]
    [MemberData(nameof(ThrowsThatDoNotSpendFailureBudget))]
    public async Task thrown_send_does_not_spend_the_failure_budget(Exception thrown)
    {
        // Arrange
        UseOptions(maxPosts: 5, maxFailures: 1);
        var first = CreatePodcastEpisode();
        var second = CreatePodcastEpisode();
        SetupPodcasts(first, second);
        _mocker.GetMock<IBlueskyPoster>()
            .Setup(x => x.Post(first, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ThrowsAsync(thrown);
        PosterReturns(second, BlueskySendStatus.Success);
        var sut = _mocker.CreateInstance<BlueskyPostManager>();

        // Act
        await sut.Post(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        VerifyPosted(first, Times.Once());
        VerifyPosted(second, Times.Once());
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetBlueskyReady(), Times.Once);
    }
}
