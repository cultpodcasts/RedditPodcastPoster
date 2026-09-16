using Api.Models;
using AutoFixture;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastRenameCommandTests
{
    private readonly Fixture _fixture = new();

    [Theory(DisplayName =
        "Rename command from a route podcast name percent-decodes without treating plus as space, because Index and GET already use that contract.")]
    [InlineData("News+Weather", "News+Weather")]
    [InlineData("News%2BWeather", "News+Weather")]
    [InlineData("News%252BWeather", "News+Weather")]
    public void route_name_is_normalized_like_index(string routeName, string expectedName)
    {
        // Arrange
        var newName = _fixture.Create<string>();

        // Act
        var command = new PodcastRenameCommand(
            PodcastRouteNameNormalizer.Normalize(routeName),
            newName);

        // Assert
        command.Name.Should().Be(expectedName);
        command.NewName.Should().Be(newName);
    }
}
