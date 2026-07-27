using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Heroes;

public class HeroAutoPromoteSelectorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Always-promote podcast with a newly indexed in-week episode: selector returns that episode id, because indexing should auto-append it to heroes.")]
    public void selects_in_week_episode_when_flagged()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = true;
        var episodeId = _fixture.CreateGuid();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Id = episodeId;
            e.Release = DomainTestFixture.UtcDaysAgo(2);
            e.Ignored = false;
            e.Removed = false;
        });

        // Act
        var ids = HeroAutoPromoteSelector.SelectEpisodeIds(
            podcast,
            [episode],
            DomainTestFixture.UtcToday);

        // Assert
        ids.Should().Equal(episodeId);
        HeroAutoPromoteSelector.GetSkipReason(podcast, episode, DomainTestFixture.UtcToday)
            .Should().Be(HeroAutoPromoteSkipReason.None);
    }

    [Fact(DisplayName =
        "Always-promote off: selector returns no ids and skip reason is FlagOff, because only flagged podcasts auto-promote.")]
    public void skips_when_flag_off()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = false;
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(1);
            e.Ignored = false;
            e.Removed = false;
        });

        // Act
        var ids = HeroAutoPromoteSelector.SelectEpisodeIds(
            podcast,
            [episode],
            DomainTestFixture.UtcToday);
        var reason = HeroAutoPromoteSelector.GetSkipReason(podcast, episode, DomainTestFixture.UtcToday);

        // Assert
        ids.Should().BeEmpty();
        reason.Should().Be(HeroAutoPromoteSkipReason.FlagOff);
    }

    [Fact(DisplayName =
        "Always-promote podcast with an episode older than one week: skip reason is OutsideWeekWindow, because heroes only cover the current week window.")]
    public void skips_episodes_older_than_week()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = true;
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(8);
            e.Ignored = false;
            e.Removed = false;
        });

        // Act
        var ids = HeroAutoPromoteSelector.SelectEpisodeIds(
            podcast,
            [episode],
            DomainTestFixture.UtcToday);
        var reason = HeroAutoPromoteSelector.GetSkipReason(podcast, episode, DomainTestFixture.UtcToday);

        // Assert
        ids.Should().BeEmpty();
        reason.Should().Be(HeroAutoPromoteSkipReason.OutsideWeekWindow);
    }

    [Fact(DisplayName =
        "Always-promote podcast with ignored newly indexed episode: skip reason is Ignored, because ignored episodes are not hero-eligible.")]
    public void skips_ignored()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = true;
        var ignored = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(1);
            e.Ignored = true;
            e.Removed = false;
        });

        // Act
        var reason = HeroAutoPromoteSelector.GetSkipReason(podcast, ignored, DomainTestFixture.UtcToday);

        // Assert
        HeroAutoPromoteSelector.SelectEpisodeIds(podcast, [ignored], DomainTestFixture.UtcToday)
            .Should().BeEmpty();
        reason.Should().Be(HeroAutoPromoteSkipReason.Ignored);
    }

    [Fact(DisplayName =
        "Always-promote podcast with removed newly indexed episode: skip reason is Removed, because removed episodes are not hero-eligible.")]
    public void skips_removed()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = true;
        var removed = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(1);
            e.Ignored = false;
            e.Removed = true;
        });

        // Act
        var reason = HeroAutoPromoteSelector.GetSkipReason(podcast, removed, DomainTestFixture.UtcToday);

        // Assert
        HeroAutoPromoteSelector.SelectEpisodeIds(podcast, [removed], DomainTestFixture.UtcToday)
            .Should().BeEmpty();
        reason.Should().Be(HeroAutoPromoteSkipReason.Removed);
    }

    [Fact(DisplayName =
        "Hero auto-promote skip diagnostic: LogSkipped emits Information with Hero auto-promote prefix and reason, because skip diagnostics are not elevated above attempt logs.")]
    public void log_skipped_emits_information_with_stable_prefix()
    {
        // Arrange
        var logger = new CapturingLogger();
        var episodeId = _fixture.CreateGuid();
        var podcastId = _fixture.CreateGuid();
        var release = DomainTestFixture.UtcDaysAgo(8);
        var cutoff = HeroAutoPromoteSelector.GetCutoff(DomainTestFixture.UtcToday);

        // Act
        HeroAutoPromoteLogger.LogSkipped(
            logger,
            HeroAutoPromoteSkipReason.OutsideWeekWindow,
            episodeId,
            podcastId,
            alwaysPromoteAsHero: true,
            release: release,
            cutoff: cutoff,
            episodeResult: "Created");

        // Assert
        logger.Informations.Should().ContainSingle(m =>
            m.StartsWith(HeroAutoPromoteLogger.MessagePrefix) &&
            m.Contains("skipped") &&
            m.Contains("OutsideWeekWindow") &&
            m.Contains(episodeId.ToString()) &&
            m.Contains(podcastId.ToString()));
        logger.Warnings.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Hero auto-promote attempt diagnostic: LogAttempt emits Information with source and episode ids, because attempt diagnostics share severity with skip logs.")]
    public void log_attempt_emits_information_with_stable_prefix()
    {
        // Arrange
        var logger = new CapturingLogger();
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();

        // Act
        HeroAutoPromoteLogger.LogAttempt(
            logger,
            EpisodeCreationSource.SubmitUrl,
            podcastId,
            [episodeId]);

        // Assert
        logger.Informations.Should().ContainSingle(m =>
            m.StartsWith(HeroAutoPromoteLogger.MessagePrefix) &&
            m.Contains("SubmitUrl") &&
            m.Contains(podcastId.ToString()) &&
            m.Contains(episodeId.ToString()));
        logger.Warnings.Should().BeEmpty();
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public List<string> Informations { get; } = [];

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
            else if (logLevel == LogLevel.Information)
            {
                Informations.Add(message);
            }
        }
    }
}
