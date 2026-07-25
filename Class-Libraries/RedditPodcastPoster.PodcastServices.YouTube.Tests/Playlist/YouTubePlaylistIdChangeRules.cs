using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// Operators must see YouTube playlist id swaps in App Insights and retain former ids on the podcast
/// so a bad curated-playlist swap can be rolled back.
/// </summary>
public class YouTubePlaylistIdChangeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When Apply is given a different YouTube playlist id, the podcast stores the new id, appends the previous " +
        "non-empty id with replaced-at to history, and logs a Warning with the stable prefix.")]
    public void Apply_changes_id_appends_history_and_logs_warning()
    {
        // Arrange
        var previousId = _fixture.CreateYouTubePlaylistId();
        var measuredId = _fixture.CreateYouTubePlaylistId();
        var replacedAt = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(15));
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = previousId);
        var logger = new CapturingLogger();

        // Act
        var changed = YouTubePlaylistIdChange.Apply(podcast, measuredId, logger, replacedAt);

        // Assert
        changed.Should().BeTrue();
        podcast.YouTubePlaylistId.Should().Be(measuredId);
        podcast.YouTubePlaylistIdHistory.Should().ContainSingle();
        podcast.YouTubePlaylistIdHistory![0].Id.Should().Be(previousId);
        podcast.YouTubePlaylistIdHistory[0].ReplacedAt.Should().Be(replacedAt);
        logger.Warnings.Should().ContainSingle(m =>
            m.StartsWith(YouTubePlaylistIdChange.ChangedMessagePrefix) &&
            m.Contains($"previous='{previousId}'") &&
            m.Contains($"measured='{measuredId}'") &&
            m.Contains($"podcast-id='{podcast.Id}'"));
    }

    [Fact(DisplayName =
        "When Apply is given the same YouTube playlist id already on the podcast, the id and history are unchanged " +
        "and no Warning is logged because identical writes are not operator-visible events.")]
    public void Apply_is_noop_when_id_unchanged()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = playlistId);
        var logger = new CapturingLogger();

        // Act
        var changed = YouTubePlaylistIdChange.Apply(podcast, playlistId, logger);

        // Assert
        changed.Should().BeFalse();
        podcast.YouTubePlaylistId.Should().Be(playlistId);
        podcast.YouTubePlaylistIdHistory.Should().BeNull();
        logger.Warnings.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When Apply clears a YouTube playlist id to empty, the previous id is retained in history with replaced-at " +
        "because dropping the release-authority playlist must still be recoverable.")]
    public void Apply_appends_history_when_playlist_id_cleared()
    {
        // Arrange
        var previousId = _fixture.CreateYouTubePlaylistId();
        var replacedAt = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = previousId);
        var logger = new CapturingLogger();

        // Act
        var changed = YouTubePlaylistIdChange.Apply(podcast, string.Empty, logger, replacedAt);

        // Assert
        changed.Should().BeTrue();
        podcast.YouTubePlaylistId.Should().BeEmpty();
        podcast.YouTubePlaylistIdHistory.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new YouTubePlaylistIdHistoryEntry
            {
                Id = previousId,
                ReplacedAt = replacedAt
            });
        logger.Warnings.Should().ContainSingle(m =>
            m.StartsWith(YouTubePlaylistIdChange.ChangedMessagePrefix) &&
            m.Contains($"previous='{previousId}'") &&
            m.Contains("measured=''"));
    }

    [Fact(DisplayName =
        "When Apply sets the first YouTube playlist id on a podcast that had none, history stays empty " +
        "because there is no former id to recover.")]
    public void Apply_does_not_append_history_when_previous_was_empty()
    {
        // Arrange
        var measuredId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = string.Empty);
        var logger = new CapturingLogger();

        // Act
        var changed = YouTubePlaylistIdChange.Apply(podcast, measuredId, logger);

        // Assert
        changed.Should().BeTrue();
        podcast.YouTubePlaylistId.Should().Be(measuredId);
        podcast.YouTubePlaylistIdHistory.Should().BeNull();
        logger.Warnings.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When Apply changes the playlist id a second time, history appends newest-last so both former ids " +
        "remain recoverable in chronological order.")]
    public void Apply_appends_history_newest_last_across_multiple_swaps()
    {
        // Arrange
        var first = _fixture.CreateYouTubePlaylistId();
        var second = _fixture.CreateYouTubePlaylistId();
        var third = _fixture.CreateYouTubePlaylistId();
        var firstReplacedAt = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(10));
        var secondReplacedAt = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(11));
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = first);

        // Act
        YouTubePlaylistIdChange.Apply(podcast, second, replacedAtUtc: firstReplacedAt);
        YouTubePlaylistIdChange.Apply(podcast, third, replacedAtUtc: secondReplacedAt);

        // Assert
        podcast.YouTubePlaylistId.Should().Be(third);
        podcast.YouTubePlaylistIdHistory.Should().HaveCount(2);
        podcast.YouTubePlaylistIdHistory![0].Id.Should().Be(first);
        podcast.YouTubePlaylistIdHistory[0].ReplacedAt.Should().Be(firstReplacedAt);
        podcast.YouTubePlaylistIdHistory[1].Id.Should().Be(second);
        podcast.YouTubePlaylistIdHistory[1].ReplacedAt.Should().Be(secondReplacedAt);
    }

    [Fact(DisplayName =
        "When Apply trims whitespace around a new playlist id, the stored value is trimmed and compared without " +
        "treating padded duplicates as changes.")]
    public void Apply_trims_measured_id_and_ignores_whitespace_only_noop()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = playlistId);
        var logger = new CapturingLogger();

        // Act
        var unchanged = YouTubePlaylistIdChange.Apply(podcast, $"  {playlistId}  ", logger);
        var clearedByWhitespace = YouTubePlaylistIdChange.Apply(
            _fixture.CreatePodcast(p => p.YouTubePlaylistId = playlistId),
            "   ",
            logger);

        // Assert
        unchanged.Should().BeFalse();
        clearedByWhitespace.Should().BeTrue();
        logger.Warnings.Should().ContainSingle();
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
