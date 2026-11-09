using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
        "X API safe-retry uses seconds-scale exponential backoff with jitter, because sub-second delays amplify load during 5xx blips.")]
    public void retry_backoff_is_seconds_scale_exponential_with_jitter()
    {
        // Arrange
        // Act
        var options = XApiSafeRetryPolicy.CreateRetryOptions();

        // Assert
        options.MaxRetryAttempts.Should().Be(XApiSafeRetryPolicy.MaxRetries);
        options.Delay.Should().Be(XApiSafeRetryPolicy.RetryBaseDelay);
        options.Delay.TotalSeconds.Should().BeGreaterThanOrEqualTo(1);
        options.BackoffType.Should().Be(Polly.DelayBackoffType.Exponential);
        options.UseJitter.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A GET that fails with 503 is retried and then succeeds, because timeline reads are idempotent.")]
    public async Task get_retries_on_5xx_then_succeeds()
    {
        // Arrange
        _inner.Respond(HttpStatusCode.ServiceUnavailable, "{}");
        _inner.Respond(HttpStatusCode.OK, "{\"data\":[],\"meta\":{}}");
        using var client = CreateResilientClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.x.com/2/users/1/tweets");

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
        using var client = CreateResilientClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "https://api.x.com/2/tweets/1");

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
        using var client = CreateResilientClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.com/2/tweets")
        {
            Content = new StringContent("{\"text\":\"x\"}")
        };

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
        using var client = CreateResilientClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.x.com/2/users/1/tweets");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _inner.Requests.Should().ContainSingle();
    }

    private HttpClient CreateResilientClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(XApiSafeRetryPolicy.PipelineName)
            .ConfigurePrimaryHttpMessageHandler(() => new NonDisposingHandler(_inner))
            .AddResilienceHandler(XApiSafeRetryPolicy.PipelineName, XApiSafeRetryPolicy.Configure);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>().CreateClient(XApiSafeRetryPolicy.PipelineName);
    }

    /// <summary>
    /// Keeps the shared <see cref="ScriptedHttpHandler"/> alive across HttpClient disposal.
    /// </summary>
    private sealed class NonDisposingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override void Dispose(bool disposing)
        {
            // Do not dispose the shared scripted inner handler.
        }
    }
}
