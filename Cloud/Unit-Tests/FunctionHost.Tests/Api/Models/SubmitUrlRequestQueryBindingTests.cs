using FluentAssertions;
using Api.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class SubmitUrlRequestQueryBindingTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "SubmitUrlRequest.TryParseUsableHttpUrl accepts an absolute https URL, because lookup uses the same gate as POST SubmitUrl.")]
    public void absolute_https_parses()
    {
        // Arrange
        var value = $"https://example.com/{_fixture.CreateGuid():N}";

        // Act
        var parsed = SubmitUrlRequest.TryParseUsableHttpUrl(value, out var url);

        // Assert
        parsed.Should().BeTrue();
        url.Should().Be(new Uri(value));
    }

    [Fact(DisplayName =
        "SubmitUrlRequest.TryParseUsableHttpUrl rejects a blank query value, because Isolated GET binds missing url as empty or null.")]
    public void blank_does_not_parse()
    {
        // Arrange
        // Act
        var parsedNull = SubmitUrlRequest.TryParseUsableHttpUrl(null, out _);
        var parsedEmpty = SubmitUrlRequest.TryParseUsableHttpUrl("   ", out _);

        // Assert
        parsedNull.Should().BeFalse();
        parsedEmpty.Should().BeFalse();
    }

    [Fact(DisplayName =
        "SubmitUrlRequest.TryParseUsableHttpUrl rejects ftp, because only http and https schemes are allowed.")]
    public void ftp_does_not_parse()
    {
        // Arrange
        // Act
        var parsed = SubmitUrlRequest.TryParseUsableHttpUrl("ftp://example.com/episode", out _);

        // Assert
        parsed.Should().BeFalse();
    }
}
