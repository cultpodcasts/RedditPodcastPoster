using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

public class YouTubeExpensiveQueryFlagRules
{
    [Fact(DisplayName =
        "A conclusive ascending probe sets YouTubePlaylistQueryIsExpensive true " +
        "because oldest-first playlists require full pagination.")]
    public void Conclusive_ascending_sets_true()
    {
        var podcast = new Podcast { YouTubePlaylistQueryIsExpensive = false };

        var changed = YouTubeExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: true,
            orderSampleSize: YouTubeExpensiveQueryFlag.MinimumOrderSampleSize);

        changed.Should().BeTrue();
        podcast.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A conclusive newest-first probe clears YouTubePlaylistQueryIsExpensive " +
        "because YouTube playlists are known to flip back from ascending order.")]
    public void Conclusive_newest_first_clears_true()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            Name = "Test Podcast",
            YouTubePlaylistId = "PL_test",
            YouTubePlaylistQueryIsExpensive = true
        };
        var logger = new CapturingLogger();

        var changed = YouTubeExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: false,
            orderSampleSize: YouTubeExpensiveQueryFlag.MinimumOrderSampleSize,
            logger);

        changed.Should().BeTrue();
        podcast.YouTubePlaylistQueryIsExpensive.Should().BeFalse();
        logger.Warnings.Should().ContainSingle(x =>
            x.StartsWith(YouTubeExpensiveQueryFlag.FlagFlippedMessagePrefix));
    }

    [Fact(DisplayName =
        "Successive conclusive probes flip YouTubePlaylistQueryIsExpensive in both directions " +
        "because a playlist that switches order must never latch on a stale value.")]
    public void Conclusive_probes_round_trip_in_both_directions()
    {
        var podcast = new Podcast { YouTubePlaylistQueryIsExpensive = false };
        var observed = new List<bool?>();

        foreach (var measured in new[] { true, false, true, false })
        {
            YouTubeExpensiveQueryFlag.Apply(
                podcast,
                measured,
                YouTubeExpensiveQueryFlag.MinimumOrderSampleSize);
            observed.Add(podcast.YouTubePlaylistQueryIsExpensive);
        }

        observed.Should().Equal(true, false, true, false);
    }

    [Fact(DisplayName =
        "An inconclusive single-item probe leaves YouTubePlaylistQueryIsExpensive unchanged " +
        "because one publish date cannot distinguish playlist order.")]
    public void Inconclusive_sample_does_not_clear_flag()
    {
        var podcast = new Podcast { YouTubePlaylistQueryIsExpensive = true };

        var changed = YouTubeExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: false,
            orderSampleSize: 1);

        changed.Should().BeFalse();
        podcast.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A null probe leaves YouTubePlaylistQueryIsExpensive unchanged " +
        "because skipped or undated lookups must not invent a playlist-order measurement.")]
    public void Null_probe_does_not_change_flag()
    {
        var podcast = new Podcast { YouTubePlaylistQueryIsExpensive = true };

        var changed = YouTubeExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: null,
            orderSampleSize: YouTubeExpensiveQueryFlag.MinimumOrderSampleSize);

        changed.Should().BeFalse();
        podcast.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
