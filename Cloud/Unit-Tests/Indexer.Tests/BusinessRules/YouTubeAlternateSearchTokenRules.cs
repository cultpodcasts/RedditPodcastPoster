using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.Search.Formatting;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class YouTubeAlternateSearchTokenRules
{
    [Fact(DisplayName =
        "Format writes {youTubeId}|{yyyy-MM-dd}|{channelName}, leaves release and channel blank when missing, keeps a | in the channel name, and converts a non-UTC release to a UTC date (RPP #1411).")]
    public void format_and_for_cover_empty_pipe_and_non_utc_release()
    {
        // Arrange
        var utcRelease = DomainTestFixture.UtcDaysAgo(10);
        var utcAtEvening = DomainTestFixture.UtcAtTime(-10, TimeSpan.FromHours(22));
        var localRelease = new DateTime(utcAtEvening.Ticks, DateTimeKind.Local);
        var empty = new YouTubeAlternate { YouTubeId = "abc", Url = new Uri("https://www.youtube.com/watch?v=abc"), AddedAt = DomainTestFixture.UtcToday };
        var piped = new YouTubeAlternate
        {
            YouTubeId = "def",
            Url = new Uri("https://www.youtube.com/watch?v=def"),
            Release = utcRelease,
            ChannelName = "True | Crime Fix",
            AddedAt = DomainTestFixture.UtcToday
        };
        var local = new YouTubeAlternate
        {
            YouTubeId = "ghi",
            Url = new Uri("https://www.youtube.com/watch?v=ghi"),
            Release = localRelease,
            ChannelName = "Channel",
            AddedAt = DomainTestFixture.UtcToday
        };
        var film = new Film("Manic")
        {
            AdditionalYouTube = [empty, piped]
        };
        var bare = new Film("Bare");

        // Act
        var emptyToken = YouTubeAlternateSearchToken.Format(empty);
        var pipedToken = YouTubeAlternateSearchToken.Format(piped);
        var localToken = YouTubeAlternateSearchToken.Format(local);
        var tokens = YouTubeAlternateSearchToken.For(film);
        var none = YouTubeAlternateSearchToken.For(bare);

        // Assert
        emptyToken.Should().Be("abc||");
        pipedToken.Should().Be($"def|{utcRelease.ToUniversalTime():yyyy-MM-dd}|True | Crime Fix");
        localToken.Should().Be($"ghi|{local.Release.Value.ToUniversalTime():yyyy-MM-dd}|Channel");
        tokens.Should().Equal(emptyToken, pipedToken);
        none.Should().BeNull();
    }
}
