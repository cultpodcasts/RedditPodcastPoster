using FluentAssertions;
using RedditPodcastPoster.Ard.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Ard.Tests.BusinessRules;

public class ArdUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An ARD /video/{show}/{episode}/{publisher}/{id} URL is a submit URL, " +
        "and the compact /video/{id} form is accepted when the id looks like a mediathek content id.")]
    public void video_urls_are_submit_urls()
    {
        // Arrange
        var id = ToContentId(_fixture.CreateGuid());
        var full = new Uri(
            $"https://www.ardmediathek.de/video/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/ard/{id}");
        var compact = new Uri($"https://www.ardmediathek.de/video/{id}");

        // Act / Assert
        ArdUrlMatcher.IsSubmitUrl(full).Should().BeTrue();
        ArdUrlMatcher.IsVideoUrl(full).Should().BeTrue();
        ArdUrlMatcher.IsSubmitUrl(compact).Should().BeTrue();
        ArdUrlMatcher.IsVideoUrl(compact).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An ARD /serie/.../{id} or /sendung/{slug}/{id} URL is a submit URL, " +
        "because those hubs identify a catalogue brand with a content id.")]
    public void serie_and_sendung_urls_are_submit_urls()
    {
        // Arrange
        var id = ToContentId(_fixture.CreateGuid());
        var serie = new Uri(
            $"https://www.ardmediathek.de/serie/{_fixture.CreateYouTubeId()}/staffel-1/{id}/1");
        var sendung = new Uri($"https://www.ardmediathek.de/sendung/{_fixture.CreateYouTubeId()}/{id}");

        // Act / Assert
        ArdUrlMatcher.IsSubmitUrl(serie).Should().BeTrue();
        ArdUrlMatcher.IsSerieUrl(serie).Should().BeTrue();
        ArdUrlMatcher.IsSubmitUrl(sendung).Should().BeTrue();
        ArdUrlMatcher.IsSendungUrl(sendung).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An ARD browse shelf such as /filme or /video without an id is not a submit URL, " +
        "because it does not identify a catalogue item.")]
    public void browse_shelves_are_not_submit_urls()
    {
        // Arrange
        var filme = new Uri("https://www.ardmediathek.de/filme");
        var videoRoot = new Uri("https://www.ardmediathek.de/video");

        // Act / Assert
        ArdUrlMatcher.IsSubmitUrl(filme).Should().BeFalse();
        ArdUrlMatcher.IsSubmitUrl(videoRoot).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters ardmediathek.de is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var id = ToContentId(_fixture.CreateGuid());
        var url = new Uri($"https://www.ardmediathek.de.example.test/video/{id}");

        // Act
        var matches = ArdUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    private static string ToContentId(Guid guid) =>
        Convert.ToBase64String(guid.ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
