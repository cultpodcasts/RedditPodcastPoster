using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Stage lines cost App Insights ingestion only when they are Warning (the level the indexer ships), so they are
/// Information by default and promoted only for traced podcasts or slow stages.
/// </summary>
public class PodcastIndexingStageLogRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RecordingLogger<PodcastsUpdater> _logger = new();
    private readonly PodcastIndexingStageLogOptions _options = new();
    private readonly Guid _podcastId;
    private readonly string _podcastName;

    public PodcastIndexingStageLogRules()
    {
        _podcastId = _fixture.CreateGuid();
        _podcastName = _fixture.CreateTitle();
    }

    [Fact(DisplayName =
        "Each stage line carries the podcast id and name, the ms since the podcast started and how long the previous " +
        "stage took, and is Information by default, because shipping every stage of every podcast would risk the " +
        "daily App Insights cap.")]
    public void stage_lines_carry_podcast_and_timings_at_information()
    {
        // Arrange
        var sut = new PodcastIndexingStageLog(_podcastId, _podcastName, _logger, _time, _options);
        var firstStageDuration = TimeSpan.FromMilliseconds(_fixture.CreateDuration().TotalSeconds);

        // Act
        sut.Stage("cosmos-read-episodes");
        _time.Advance(firstStageDuration);
        sut.Stage("youtube-playlist-page", "page=1");

        // Assert
        sut.CurrentStage.Should().Be("youtube-playlist-page");
        sut.CurrentStageDetail.Should().Be("page=1");
        _logger.Entries.Should().HaveCount(2).And.OnlyContain(entry => entry.Level == LogLevel.Information);
        var last = _logger.Entries[1].Message;
        last.Should().StartWith(PodcastIndexingStageLog.MessagePrefix)
            .And.Contain($"podcast-id='{_podcastId}'")
            .And.Contain($"podcast-name='{_podcastName}'")
            .And.Contain("stage='youtube-playlist-page'")
            .And.Contain($"elapsed-ms='{(long) firstStageDuration.TotalMilliseconds}'")
            .And.Contain("previous-stage='cosmos-read-episodes'")
            .And.Contain($"previous-stage-ms='{(long) firstStageDuration.TotalMilliseconds}'");
    }

    [Fact(DisplayName =
        "A podcast on the Warning allowlist logs every stage start at Warning, because a call that hangs never logs an " +
        "end and the last shipped start line must name it.")]
    public void allowlisted_podcast_logs_every_stage_at_warning()
    {
        // Arrange
        _options.WarningPodcastIds = $"{_fixture.CreateGuid()}, {_podcastId}";
        var sut = new PodcastIndexingStageLog(_podcastId, _podcastName, _logger, _time, _options);

        // Act
        sut.Stage("start");
        sut.Stage("cosmos-read-episodes");

        // Assert
        _logger.Entries.Should().HaveCount(2).And.OnlyContain(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("traced='True'"));
    }

    [Fact(DisplayName =
        "The allowlist accepts an id prefix of at least 8 characters, so the podcast can be named by the short id seen " +
        "in logs; a shorter prefix is ignored so a typo cannot trace a large share of podcasts.")]
    public void allowlist_accepts_long_prefixes_only()
    {
        // Arrange
        var id = _podcastId.ToString("D");
        var longPrefix = new PodcastIndexingStageLogOptions { WarningPodcastIds = id[..8].ToUpperInvariant() };
        var shortPrefix = new PodcastIndexingStageLogOptions { WarningPodcastIds = id[..7] };

        // Act
        var longMatches = longPrefix.IsWarningPodcast(_podcastId);
        var shortMatches = shortPrefix.IsWarningPodcast(_podcastId);

        // Assert
        longMatches.Should().BeTrue();
        shortMatches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "The line that follows a stage slower than the threshold is Warning and flags the slow stage, because a slow " +
        "call is worth shipping even for a podcast that is not traced.")]
    public void slow_stage_is_logged_at_warning()
    {
        // Arrange
        var sut = new PodcastIndexingStageLog(_podcastId, _podcastName, _logger, _time, _options);

        // Act
        sut.Stage("discover-youtube");
        _time.Advance(TimeSpan.FromSeconds(PodcastIndexingStageLogOptions.DefaultSlowStageWarningSeconds));
        sut.Stage("merge");

        // Assert
        _logger.Entries[0].Level.Should().Be(LogLevel.Information);
        _logger.Entries[1].Level.Should().Be(LogLevel.Warning);
        _logger.Entries[1].Message.Should().Contain("previous-stage='discover-youtube'")
            .And.Contain("slow-previous-stage='True'");
    }
}
