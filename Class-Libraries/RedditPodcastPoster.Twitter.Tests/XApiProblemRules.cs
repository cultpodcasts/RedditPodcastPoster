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
        problem.Summary().Should().BeEmpty();
    }
}
