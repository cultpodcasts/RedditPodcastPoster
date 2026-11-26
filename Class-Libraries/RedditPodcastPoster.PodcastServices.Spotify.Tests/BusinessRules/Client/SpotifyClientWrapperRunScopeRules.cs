using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Client;

/// <summary>
/// Spotify calls made inside a podcast's indexer run are cancelled by that podcast's budget, which they read from the
/// ambient <see cref="PodcastRunScope"/> (#1073). A budget cut is not a Spotify failure: it propagates to the indexer
/// loop and is neither logged as an error nor turned into <see cref="IndexingContext.SkipSpotifyUrlResolving"/>.
/// </summary>
public class SpotifyClientWrapperRunScopeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<SpotifyClientWrapper> _logger = new();
    private readonly CancellationTokenSource _budget = new();
    private readonly TaskCompletionSource _callStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SpotifyClientWrapperRunScopeRules()
    {
        _mocker.Use<ILogger<SpotifyClientWrapper>>(_logger);
        var client = _mocker.GetMock<ISpotifyClient>();
        client
            .Setup(x => x.Episodes.Get(It.IsAny<string>(), It.IsAny<EpisodeRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, EpisodeRequest _, CancellationToken token) =>
            {
                _callStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new FullEpisode();
            });
        _mocker.GetMock<IAsyncInstance<ISpotifyClient>>()
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => client.Object);
    }

    [Fact(DisplayName =
        "When a Spotify call is made inside a podcast's run whose budget is already spent, the cancellation " +
        "propagates and nothing is logged as an error or marks Spotify as skipped, because a budget cut is reported " +
        "by the indexer loop and is not a Spotify failure.")]
    public async Task spent_budget_cancellation_propagates_without_a_spotify_failure()
    {
        // Arrange
        await _budget.CancelAsync();
        var context = new IndexingContext();
        var sut = _mocker.CreateInstance<SpotifyClientWrapper>();

        // Act
        Task<FullEpisode?> call;
        using (PodcastRunScope.Enter(new PodcastRunScope(_budget.Token)))
        {
            call = sut.GetFullEpisode(_fixture.CreateSpotifyId(), new EpisodeRequest(), context);
        }

        // Assert
        await call.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
        _logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Error);
        context.SkipSpotifyUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a Spotify call inside a podcast's run also carries the caller's own cancellable token, the podcast's " +
        "budget still cancels the call mid-flight, because the wrapper links the caller's token with the run's budget.")]
    public async Task budget_cancels_a_call_that_carries_its_own_token()
    {
        // Arrange
        using var callerToken = new CancellationTokenSource();
        var context = new IndexingContext();
        var sut = _mocker.CreateInstance<SpotifyClientWrapper>();
        Task<FullEpisode?> call;
        using (PodcastRunScope.Enter(new PodcastRunScope(_budget.Token)))
        {
            call = sut.GetFullEpisode(_fixture.CreateSpotifyId(), new EpisodeRequest(), context, callerToken.Token);
        }

        await _callStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Act
        await _budget.CancelAsync();

        // Assert
        await call.Awaiting(task => task.WaitAsync(TimeSpan.FromSeconds(10)))
            .Should().ThrowAsync<OperationCanceledException>();
        callerToken.IsCancellationRequested.Should().BeFalse();
        _logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Error);
        context.SkipSpotifyUrlResolving.Should().BeFalse();
    }
}
