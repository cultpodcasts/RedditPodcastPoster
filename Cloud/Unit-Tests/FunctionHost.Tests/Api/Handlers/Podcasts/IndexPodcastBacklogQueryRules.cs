using Api.Handlers.Podcasts;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers.Podcasts;

public class IndexPodcastBacklogQueryRules
{
    [Theory(DisplayName =
        "POST podcast/index backlog query is true only for true / 1 / yes (case-insensitive); absent or other values stay short lookback.")]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    public void backlog_query_truthiness(string? value, bool expected)
    {
        // Arrange

        // Act
        var actual = IndexPodcastHandler.IsTruthy(value);

        // Assert
        actual.Should().Be(expected);
    }
}
