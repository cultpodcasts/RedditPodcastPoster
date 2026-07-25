using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// Operators must see YouTube playlist id swaps in App Insights (e.g. unlisted → public show playlist)
/// because the configured playlist is release-authority for enrichment and discovery.
/// </summary>
public class YouTubePlaylistIdChangeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When Apply is given a different YouTube playlist id, the podcast stores the new id and a Warning is logged " +
        "with the stable prefix so operators can see curated playlist swaps in App Insights.")]
    public void Apply_changes_id_and_logs_warning_with_stable_prefix()
    {
        // Arrange
        var previousId = _fixture.CreateYouTubePlaylistId();
        var measuredId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = previousId);
        var logger = new CapturingLogger();

        // Act
        var changed = YouTubePlaylistIdChange.Apply(podcast, measuredId, logger);

        // Assert
        changed.Should().BeTrue();
        podcast.YouTubePlaylistId.Should().Be(measuredId);
        logger.Warnings.Should().ContainSingle(m =>
            m.StartsWith(YouTubePlaylistIdChange.ChangedMessagePrefix) &&
            m.Contains($"previous='{previousId}'") &&
            m.Contains($"measured='{measuredId}'") &&
            m.Contains($"podcast-id='{podcast.Id}'"));
    }

    [Fact(DisplayName =
        "When Apply is given the same YouTube playlist id already on the podcast, the id is unchanged and no Warning " +
        "is logged because identical writes are not operator-visible events.")]
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
        logger.Warnings.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When Apply clears a YouTube playlist id to empty, the podcast stores empty and a Warning records the previous id " +
        "because dropping the release-authority playlist is an operator-visible configuration change.")]
    public void Apply_logs_when_playlist_id_cleared()
    {
        // Arrange
        var previousId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = previousId);
        var logger = new CapturingLogger();

        // Act
        var changed = YouTubePlaylistIdChange.Apply(podcast, string.Empty, logger);

        // Assert
        changed.Should().BeTrue();
        podcast.YouTubePlaylistId.Should().BeEmpty();
        logger.Warnings.Should().ContainSingle(m =>
            m.StartsWith(YouTubePlaylistIdChange.ChangedMessagePrefix) &&
            m.Contains($"previous='{previousId}'") &&
            m.Contains("measured=''"));
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
