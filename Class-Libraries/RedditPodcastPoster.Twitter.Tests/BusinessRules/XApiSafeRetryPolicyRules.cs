using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Http;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Tests.Fakes;

namespace RedditPodcastPoster.Twitter.Tests.BusinessRules;

public class XApiSafeRetryPolicyRules
{
    private readonly ScriptedHttpHandler _inner = new();

    [Theory(DisplayName =
        "Only GET and DELETE are safe to auto-retry against X; POST create must never be retried because X may already have accepted the post.")]
    [InlineData("GET", true)]
    [InlineData("DELETE", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    public void only_get_and_delete_are_safe_to_retry(string methodName, bool expected)
    {
        // Arrange
        var method = new HttpMethod(methodName);

        // Act
        var safe = XApiSafeRetryPolicy.IsSafeToRetry(method);

        // Assert
        safe.Should().Be(expected);
    }

    [Fact(DisplayName =
        "A GET that fails with 503 is retried and then succeeds, because timeline reads are idempotent.")]
    public async Task get_retries_on_5xx_then_succeeds()
    {
        // Arrange
        _inner.Respond(HttpStatusCode.ServiceUnavailable, "{}");
        _inner.Respond(HttpStatusCode.OK, "{\"data\":[],\"meta\":{}}");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.x.com/2/users/1/tweets");
        using var pipeline = new PolicyHttpMessageHandler(XApiSafeRetryPolicy.ForRequest(request))
        {
            InnerHandler = _inner
        };
        using var client = new HttpClient(pipeline);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _inner.Requests.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "A DELETE that fails with 502 is retried and then succeeds, because deleting by id is idempotent.")]
    public async Task delete_retries_on_5xx_then_succeeds()
    {
        // Arrange
        _inner.Respond(HttpStatusCode.BadGateway, "{}");
        _inner.Respond(HttpStatusCode.OK, "{\"data\":{\"deleted\":true}}");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "https://api.x.com/2/tweets/1");
        using var pipeline = new PolicyHttpMessageHandler(XApiSafeRetryPolicy.ForRequest(request))
        {
            InnerHandler = _inner
        };
        using var client = new HttpClient(pipeline);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _inner.Requests.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "A POST create that fails with 503 is not retried, so a post X already accepted is not sent twice.")]
    public async Task post_create_is_never_auto_retried_on_5xx()
    {
        // Arrange
        _inner.Respond(HttpStatusCode.ServiceUnavailable, "{}");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.com/2/tweets")
        {
            Content = new StringContent("{\"text\":\"x\"}")
        };
        using var pipeline = new PolicyHttpMessageHandler(XApiSafeRetryPolicy.ForRequest(request))
        {
            InnerHandler = _inner
        };
        using var client = new HttpClient(pipeline);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        _inner.Requests.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A GET that fails with 404 is not retried, because 4xx is not a transient X failure.")]
    public async Task get_does_not_retry_4xx()
    {
        // Arrange
        _inner.Respond(HttpStatusCode.NotFound, "{}");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.x.com/2/users/1/tweets");
        using var pipeline = new PolicyHttpMessageHandler(XApiSafeRetryPolicy.ForRequest(request))
        {
            InnerHandler = _inner
        };
        using var client = new HttpClient(pipeline);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _inner.Requests.Should().ContainSingle();
    }
}
