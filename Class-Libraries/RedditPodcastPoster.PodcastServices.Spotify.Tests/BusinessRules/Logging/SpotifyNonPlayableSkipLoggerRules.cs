using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Logging;

public class SpotifyNonPlayableSkipLoggerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When restrictions.reason is market, Log emits Error with the market-unavailable prefix " +
        "because market blocks must not be silent Warning-level skips.")]
    public void market_restriction_logs_error()
    {
        // Arrange
        var logger = new CapturingLogger();
        var episode = new FullEpisodeWithRestrictions
        {
            Id = _fixture.CreateSpotifyId(),
            Name = "Virginia | Ep 3: Mommy still loves you",
            IsPlayable = false,
            Restrictions = new Dictionary<string, string>
            {
                ["reason"] = SpotifyNonPlayableSkipLogger.MarketRestrictionReason
            }
        };

        // Act
        SpotifyNonPlayableSkipLogger.Log(logger, episode, "GB");

        // Assert
        logger.Errors.Should().ContainSingle(m =>
            m.Contains(SpotifyNonPlayableSkipLogger.MarketUnavailableMessagePrefix) &&
            m.Contains(episode.Id) &&
            m.Contains("market='GB'") &&
            m.Contains("restrictions.reason='market'"));
        logger.Warnings.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When restrictions.reason is payment_required, Log emits Warning (not Error) " +
        "because paywall skips are expected catalogue filtering.")]
    public void payment_required_logs_warning()
    {
        // Arrange
        var logger = new CapturingLogger();
        var episode = new SimpleEpisodeWithRestrictions
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            IsPlayable = false,
            Restrictions = new Dictionary<string, string> { ["reason"] = "payment_required" }
        };

        // Act
        SpotifyNonPlayableSkipLogger.Log(logger, episode, "GB");

        // Assert
        logger.Warnings.Should().ContainSingle(m =>
            m.Contains(episode.Id) &&
            m.Contains("payment_required") &&
            m.Contains("market='GB'"));
        logger.Errors.Should().BeEmpty();
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(message);
            }
            else if (logLevel == LogLevel.Error)
            {
                Errors.Add(message);
            }
        }
    }
}
