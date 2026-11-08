using System.Xml.Linq;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.YouTubePushNotifications.Adaptors;

namespace RedditPodcastPoster.YouTubePushNotifications.Tests;

public class NotificationAdaptorUtcRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "When a push notification carries published and updated times with an offset, then they are read as the same instants in UTC, because the host's time zone must never leak into stored release times.")]
    public void published_and_updated_are_read_as_utc()
    {
        // Arrange
        var published = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(14));
        var updated = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(9));
        var channelId = _fixture.CreateYouTubeId();
        var videoId = _fixture.CreateYouTubeId();
        var title = _fixture.CreateTitle();
        var xml = XDocument.Parse($"""
            <?xml version='1.0' encoding='UTF-8'?>
            <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns="http://www.w3.org/2005/Atom">
              <id>yt:channel:{channelId}</id>
              <yt:channelId>{channelId}</yt:channelId>
              <title>{title}</title>
              <author><name>{title}</name></author>
              <published>{published.AddHours(1):yyyy-MM-ddTHH:mm:ss}+01:00</published>
              <entry>
                <id>yt:video:{videoId}</id>
                <yt:videoId>{videoId}</yt:videoId>
                <yt:channelId>{channelId}</yt:channelId>
                <title>{title}</title>
                <author><name>{title}</name></author>
                <published>{published:yyyy-MM-ddTHH:mm:ss}+00:00</published>
                <updated>{updated.AddHours(-5):yyyy-MM-ddTHH:mm:ss}-05:00</updated>
              </entry>
            </feed>
            """);
        var sut = _mocker.CreateInstance<NotificationAdaptor>();

        // Act
        var result = sut.Adapt(xml);

        // Assert
        result.Published.Should().Be(published);
        result.Published!.Value.Kind.Should().Be(DateTimeKind.Utc);
        var entry = result.Entries.Should().ContainSingle().Subject;
        entry.Published.Should().Be(published);
        entry.Published!.Value.Kind.Should().Be(DateTimeKind.Utc);
        entry.Updated.Should().Be(updated);
        entry.Updated!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
