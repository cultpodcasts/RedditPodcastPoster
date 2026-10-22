using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class PodcastIndexingStageLogRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RecordingLogger<PodcastsUpdater> _logger = new();

    [Fact(DisplayName =
        "Each stage is logged at Warning with the podcast id and name, the ms since the podcast started and how long the " +
        "previous stage took, because the last stage line before a stall names the call that hung.")]
    public void stage_lines_carry_podcast_and_timings()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var podcastName = _fixture.CreateTitle();
        var sut = new PodcastIndexingStageLog(podcastId, podcastName, _logger, _time);
        var firstStageDuration = TimeSpan.FromMilliseconds(_fixture.CreateDuration().TotalSeconds);

        // Act
        sut.Stage("cosmos-read-episodes");
        _time.Advance(firstStageDuration);
        sut.Stage("youtube-playlist-page", "page=1");

        // Assert
        sut.CurrentStage.Should().Be("youtube-playlist-page");
        sut.CurrentStageDetail.Should().Be("page=1");
        _logger.Entries.Should().HaveCount(2).And.OnlyContain(entry => entry.Level == LogLevel.Warning);
        var last = _logger.Entries[1].Message;
        last.Should().StartWith(PodcastIndexingStageLog.MessagePrefix)
            .And.Contain($"podcast-id='{podcastId}'")
            .And.Contain($"podcast-name='{podcastName}'")
            .And.Contain("stage='youtube-playlist-page'")
            .And.Contain($"elapsed-ms='{(long) firstStageDuration.TotalMilliseconds}'")
            .And.Contain("previous-stage='cosmos-read-episodes'")
            .And.Contain($"previous-stage-ms='{(long) firstStageDuration.TotalMilliseconds}'");
    }
}
