using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.EpisodeLookup.Tests.Unit;

/// <summary>The fixed 10 s per-request guard shared by Spotify and Apple, in every flow.</summary>
public class CatalogueRequestGuardTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly RecordingLogger<CatalogueRequestGuard> _logger = new();

    private CatalogueRequestGuard Sut(EpisodeLookupOptions? options = null) =>
        new(_time, Options.Create(options ?? new EpisodeLookupOptions()), _logger);

    [Fact(DisplayName = "The timeout defaults to 10 seconds for every flow and platform and follows EpisodeLookupOptions.RequestTimeout.")]
    public void Timeout_comes_from_options()
    {
        // Arrange
        var configured = Sut(new EpisodeLookupOptions { RequestTimeout = TimeSpan.FromSeconds(25) });

        // Act
        var defaults = Sut();

        // Assert
        defaults.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        configured.Timeout.Should().Be(TimeSpan.FromSeconds(25));
    }

    [Fact(DisplayName = "A request that completes in time returns its result and logs nothing.")]
    public async Task Completed_request_returns_result()
    {
        // Arrange
        var sut = Sut();

        // Act
        var result = await sut.Run<string>("Spotify", "op", _ => Task.FromResult<string?>("ok"), CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        _logger.Entries.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a request runs past 10 seconds, the guard cancels it, returns null (a clean miss) and logs a warning naming platform and operation.")]
    public async Task Timed_out_request_returns_null_and_logs()
    {
        // Arrange
        var sut = Sut();
        var started = new TaskCompletionSource();

        // Act
        var run = sut.Run<string>("Apple", "GetEpisodePage podcast=1603773245", async ct =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return "late";
        }, CancellationToken.None);
        await started.Task;
        _time.Advance(TimeSpan.FromSeconds(10));
        var result = await run;

        // Assert
        result.Should().BeNull();
        _logger.Entries.Should().ContainSingle(x =>
            x.Level == LogLevel.Warning &&
            x.Message.StartsWith(CatalogueRequestGuard.GuardFiredMessagePrefix) &&
            x.Message.Contains("platform='Apple'") &&
            x.Message.Contains("GetEpisodePage podcast=1603773245"));
    }

    [Fact(DisplayName = "Caller cancellation is not swallowed: the OperationCanceledException propagates and no timeout is logged.")]
    public async Task Caller_cancellation_propagates()
    {
        // Arrange
        var sut = Sut();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => sut.Run<string>("Spotify", "op", async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return null;
        }, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Entries.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A cancellation that is not the guard's (e.g. HttpClient's own timeout) returns null and logs a separate warning, not the guard prefix.")]
    public async Task Other_cancellation_is_logged_separately()
    {
        // Arrange
        var sut = Sut();

        // Act
        var result = await sut.Run<string>("Spotify", "op", _ => throw new TaskCanceledException(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _logger.Entries.Should().ContainSingle(x => x.Message.StartsWith(CatalogueRequestGuard.OtherCancellationMessagePrefix));
    }
}
