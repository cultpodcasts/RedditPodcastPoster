using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Models;

public class SpotifyExpensiveQueryFlagRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A conclusive ascending probe sets SpotifyEpisodesQueryIsExpensive true " +
        "because oldest-first catalogues must use the end-jump pagination path.")]
    public void Conclusive_ascending_sets_true()
    {
        var podcast = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = false);

        var changed = SpotifyExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: true,
            orderSampleSize: SpotifyExpensiveQueryFlag.MinimumOrderSampleSize);

        changed.Should().BeTrue();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A conclusive newest-first probe clears SpotifyEpisodesQueryIsExpensive " +
        "because Spotify catalogues are known to flip back from ascending order.")]
    public void Conclusive_newest_first_clears_true()
    {
        var podcast = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = true);

        var changed = SpotifyExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: false,
            orderSampleSize: SpotifyExpensiveQueryFlag.MinimumOrderSampleSize,
            NullLogger.Instance);

        changed.Should().BeTrue();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeFalse();
    }

    [Fact(DisplayName =
        "An inconclusive single-episode probe leaves SpotifyEpisodesQueryIsExpensive unchanged " +
        "because one release date cannot distinguish catalogue order.")]
    public void Inconclusive_sample_does_not_clear_flag()
    {
        var podcast = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = true);

        var changed = SpotifyExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: false,
            orderSampleSize: 1);

        changed.Should().BeFalse();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A null probe leaves SpotifyEpisodesQueryIsExpensive unchanged " +
        "because skipped pagination must not invent a catalogue-order measurement.")]
    public void Null_probe_does_not_change_flag()
    {
        var podcast = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = true);

        var changed = SpotifyExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: null,
            orderSampleSize: SpotifyExpensiveQueryFlag.MinimumOrderSampleSize);

        changed.Should().BeFalse();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Successive conclusive probes flip SpotifyEpisodesQueryIsExpensive in both directions repeatedly, logging each flip " +
        "because a show that switches catalogue order more than once must never latch on a stale value.")]
    public void Conclusive_probes_round_trip_in_both_directions()
    {
        var podcast = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = false);
        var logger = new CapturingLogger();

        var observed = new List<(bool Changed, bool? Value)>();
        foreach (var measured in new[] { true, false, true, false })
        {
            var changed = ApplyConclusive(podcast, measured, logger);
            observed.Add((changed, podcast.SpotifyEpisodesQueryIsExpensive));
        }

        observed.Should().Equal(
            (true, true),
            (true, false),
            (true, true),
            (true, false));
        logger.Warnings.Should().HaveCount(4);
        logger.Warnings.Should().OnlyContain(x =>
            x.StartsWith(SpotifyExpensiveQueryFlag.FlagFlippedMessagePrefix));
    }

    [Fact(DisplayName =
        "Repeating the same conclusive probe reports no change and logs nothing " +
        "because a steady catalogue order must not emit a flip per indexer pass.")]
    public void Repeated_conclusive_probe_is_idempotent()
    {
        var podcast = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = false);
        var logger = new CapturingLogger();

        var firstChange = ApplyConclusive(podcast, measuredExpensive: true, logger);
        var secondChange = ApplyConclusive(podcast, measuredExpensive: true, logger);
        var thirdChange = ApplyConclusive(podcast, measuredExpensive: true, logger);

        firstChange.Should().BeTrue();
        secondChange.Should().BeFalse();
        thirdChange.Should().BeFalse();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
        logger.Warnings.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "An inconclusive probe between two conclusive probes preserves the flag and does not block the later flip " +
        "because a thin sample must pause measurement rather than reset it.")]
    public void Inconclusive_probe_between_flips_preserves_round_trip()
    {
        var podcast = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = false);
        var logger = new CapturingLogger();

        ApplyConclusive(podcast, measuredExpensive: true, logger);
        var inconclusiveChange = SpotifyExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive: false,
            orderSampleSize: SpotifyExpensiveQueryFlag.MinimumOrderSampleSize - 1,
            logger);
        var afterInconclusive = podcast.SpotifyEpisodesQueryIsExpensive;
        var laterChange = ApplyConclusive(podcast, measuredExpensive: false, logger);

        inconclusiveChange.Should().BeFalse();
        afterInconclusive.Should().BeTrue();
        laterChange.Should().BeTrue();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeFalse();
        logger.Warnings.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "An unmeasured podcast adopts its first conclusive probe in either direction " +
        "because a null flag is absence of measurement, not a measured newest-first result.")]
    public void Unmeasured_flag_adopts_first_conclusive_probe()
    {
        var ascending = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = null);
        var newestFirst = _fixture.CreatePodcast(p => p.SpotifyEpisodesQueryIsExpensive = null);

        var ascendingChange = ApplyConclusive(ascending, measuredExpensive: true);
        var newestFirstChange = ApplyConclusive(newestFirst, measuredExpensive: false);

        ascendingChange.Should().BeTrue();
        ascending.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
        newestFirstChange.Should().BeTrue();
        newestFirst.SpotifyEpisodesQueryIsExpensive.Should().BeFalse();
    }

    private static bool ApplyConclusive(Podcast podcast, bool measuredExpensive, ILogger? logger = null) =>
        SpotifyExpensiveQueryFlag.Apply(
            podcast,
            measuredExpensive,
            SpotifyExpensiveQueryFlag.MinimumOrderSampleSize,
            logger);

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
