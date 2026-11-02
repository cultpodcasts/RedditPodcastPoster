using FluentAssertions;
using RedditPodcastPoster.Twitter.Clients;

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
        problem.IsRateOrUsageLimited.Should().BeFalse();
        problem.IsResourceNotFound.Should().BeFalse();
        problem.Summary().Should().BeEmpty();
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
}
