using System.Net;
using FluentAssertions;
using RedditPodcastPoster.EdgeApi.Clients;
using Xunit;

namespace FunctionHost.Tests.BusinessRules.Heroes;

public class CloudflareChallengeResponseRules
{
    [Fact(DisplayName =
        "AppendHeroEpisodes response with Forbidden HTML containing Just a moment: detector treats it as Cloudflare bot challenge, because Bot Fight Mode never reached the Worker.")]
    public void forbidden_just_a_moment_html_is_bot_challenge()
    {
        // Arrange
        const string body =
            "<!DOCTYPE html><html><title>Just a moment...</title><div id=\"cf-challenge\">Checking your browser</div></html>";

        // Act
        var isChallenge = CloudflareChallengeResponse.LooksLikeBotChallenge(HttpStatusCode.Forbidden, body);
        var exception = CloudflareChallengeResponse.CreateException(HttpStatusCode.Forbidden, body);

        // Assert
        isChallenge.Should().BeTrue();
        exception.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("Cloudflare bot-mode challenge");
        exception.Message.Should().Contain("403");
        exception.Message.Should().Contain("Just a moment");
    }

    [Fact(DisplayName =
        "AppendHeroEpisodes response with Forbidden JSON app body: detector does not treat it as bot challenge, because ordinary API Forbidden must stay a plain Error log.")]
    public void forbidden_json_body_is_not_bot_challenge()
    {
        // Arrange
        const string body = """{"error":"Forbidden","message":"Missing scope"}""";

        // Act
        var isChallenge = CloudflareChallengeResponse.LooksLikeBotChallenge(HttpStatusCode.Forbidden, body);

        // Assert
        isChallenge.Should().BeFalse();
    }

    [Fact(DisplayName =
        "AppendHeroEpisodes challenge exception truncates long HTML bodies, because App Insights exception messages must stay bounded.")]
    public void create_exception_truncates_long_body()
    {
        // Arrange
        var body = "<html>Just a moment..." + new string('x', CloudflareChallengeResponse.MaxBodyCharsInLog + 50);

        // Act
        var truncated = CloudflareChallengeResponse.TruncateBody(body);
        var exception = CloudflareChallengeResponse.CreateException(HttpStatusCode.Forbidden, body);

        // Assert
        truncated.Length.Should().Be(CloudflareChallengeResponse.MaxBodyCharsInLog + 1);
        truncated.Should().EndWith("…");
        truncated.Length.Should().BeLessThan(body.Length);
        exception.Message.Should().Contain("…");
        exception.Message.Should().NotContain(body);
    }
}
