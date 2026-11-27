using System.Net;
using FluentAssertions;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Tests;

public class XApiProblemRules
{
    [Fact(DisplayName = "The legacy errors[] shape with code 187 is recognised as duplicate content.")]
    public void legacy_code_187_is_duplicate()
    {
        // Arrange
        const string body = "{\"errors\":[{\"code\":187,\"message\":\"Status is a duplicate.\"}]}";

        // Act
        var problem = XApiProblem.Parse(body);

        // Assert
        problem.IsDuplicateContent.Should().BeTrue();
    }

    [Fact(DisplayName = "An empty or invalid body parses to an empty problem rather than throwing.")]
    public void invalid_body_is_empty_problem()
    {
        // Arrange
        const string body = "not json";

        // Act
        var problem = XApiProblem.Parse(body);

        // Assert
        problem.IsDuplicateContent.Should().BeFalse();
        problem.IsUsageCapped.Should().BeFalse();
        problem.IsRateOrUsageLimited.Should().BeFalse();
        problem.IsResourceNotFound.Should().BeFalse();
        problem.Summary().Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A usage-capped problem type is recognised as usage-capped (and therefore rate-or-usage-limited), so the client can persist a longer skip than a short rate-limit window.")]
    public void usage_capped_type_is_recognised()
    {
        // Arrange
        var body = $"{{\"title\":\"UsageCapExceeded\",\"type\":\"{XApiProblem.UsageCappedType}\",\"status\":429}}";

        // Act
        var problem = XApiProblem.Parse(body);

        // Assert
        problem.IsUsageCapped.Should().BeTrue();
        problem.IsRateOrUsageLimited.Should().BeTrue();
    }

    [Theory(DisplayName =
        "A usage-capped or rate-limit-exceeded problem type or title, at the top level or inside errors[], is recognised from the parsed JSON fields, so the client classifies the failure without searching free text.")]
    [InlineData("{\"type\":\"https://api.twitter.com/2/problems/rate-limit-exceeded\"}", false, true)]
    [InlineData("{\"errors\":[{\"type\":\"https://api.x.com/2/problems/rate-limit-exceeded\"}]}", false, true)]
    [InlineData("{\"title\":\"Too Many Requests\",\"status\":429}", false, true)]
    [InlineData("{\"errors\":[{\"type\":\"https://api.twitter.com/2/problems/usage-capped\"}]}", true, false)]
    [InlineData("{\"errors\":[{\"title\":\"UsageCapExceeded\"}]}", true, false)]
    public void limit_shapes_are_read_from_json_fields(string body, bool usageCapped, bool rateLimited)
    {
        // Arrange
        var responseBody = body;

        // Act
        var problem = XApiProblem.Parse(responseBody);

        // Assert
        problem.IsUsageCapped.Should().Be(usageCapped);
        problem.IsRateLimitExceeded.Should().Be(rateLimited);
        problem.IsRateOrUsageLimited.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Free text that merely mentions too many requests in detail is not a rate limit, because the type and title fields are the contract and substring matches misclassify unrelated errors.")]
    public void free_text_alone_is_not_a_rate_limit()
    {
        // Arrange
        const string body =
            "{\"title\":\"Forbidden\",\"detail\":\"This looks like a too many requests message.\",\"status\":403}";

        // Act
        var problem = XApiProblem.Parse(body);

        // Assert
        problem.IsRateLimitExceeded.Should().BeFalse();
        problem.IsRateOrUsageLimited.Should().BeFalse();
    }

    [Theory(DisplayName =
        "A client-forbidden or not-authorized-for-resource problem type is recognised as client-forbidden, so a refusal that applies to every post stops the run.")]
    [InlineData("{\"type\":\"https://api.twitter.com/2/problems/client-forbidden\"}")]
    [InlineData("{\"errors\":[{\"type\":\"https://api.twitter.com/2/problems/not-authorized-for-resource\"}]}")]
    public void client_forbidden_shapes_are_recognised(string body)
    {
        // Arrange
        var responseBody = body;

        // Act
        var problem = XApiProblem.Parse(responseBody);

        // Assert
        problem.IsClientForbidden.Should().BeTrue();
    }

    [Theory(DisplayName =
        "A resource-not-found problem type (api.twitter.com or api.x.com) or a Not Found Error title is recognised so delete can clear Tweeted only when X says the post is gone.")]
    [InlineData("{\"type\":\"https://api.twitter.com/2/problems/resource-not-found\"}")]
    [InlineData("{\"type\":\"https://api.x.com/2/problems/resource-not-found\",\"title\":\"Not Found Error\"}")]
    [InlineData("{\"title\":\"Not Found Error\",\"detail\":\"synthetic\"}")]
    [InlineData("{\"errors\":[{\"type\":\"https://api.twitter.com/2/problems/resource-not-found\"}]}")]
    [InlineData("{\"errors\":[{\"type\":\"https://api.x.com/2/problems/resource-not-found\"}]}")]
    [InlineData("{\"errors\":[{\"title\":\"Not Found Error\"}]}")]
    public void resource_not_found_shapes_are_recognised(string body)
    {
        // Arrange
        var responseBody = body;

        // Act
        var problem = XApiProblem.Parse(responseBody);

        // Assert
        problem.IsResourceNotFound.Should().BeTrue();
    }

    [Fact(DisplayName =
        "An unrelated problem type is not treated as resource-not-found, because only an X not-found signal may clear Tweeted.")]
    public void unrelated_problem_type_is_not_resource_not_found()
    {
        // Arrange
        const string body = "{\"type\":\"https://api.twitter.com/2/problems/client-forbidden\",\"title\":\"Forbidden\"}";

        // Act
        var problem = XApiProblem.Parse(body);

        // Assert
        problem.IsResourceNotFound.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A bare HTTP 402 with an empty body is a limit-or-usage response, because credits-gone must persist a LookUps skip even without a matching problem type.")]
    public void bare_402_is_limit_or_usage_response()
    {
        // Arrange
        var problem = XApiProblem.Parse(string.Empty);

        // Act
        var isLimit = problem.IsLimitOrUsageResponse(HttpStatusCode.PaymentRequired);

        // Assert
        isLimit.Should().BeTrue();
    }

    [Fact(DisplayName =
        "TweetResponseReader maps a bare HTTP 402 delete response to TooManyRequests, so the client can persist a usage-cap skip instead of treating credits-gone as Failed.")]
    public void delete_reader_maps_bare_402_to_too_many_requests()
    {
        // Arrange
        const string body = "";

        // Act
        var state = TweetResponseReader.ReadDeleteState(HttpStatusCode.PaymentRequired, body);

        // Assert
        state.Should().Be(DeleteTweetState.TooManyRequests);
    }
}
