using Api.Models;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastRouteNameNormalizerTests
{
    [Theory(DisplayName =
        "Route podcast name is percent-decoded without treating plus as space, because form-urlencoded decode would look up a different show.")]
    [InlineData("News+Weather", "News+Weather")]
    [InlineData("News%2BWeather", "News+Weather")]
    [InlineData("News%252BWeather", "News+Weather")]
    [InlineData("News%20Weather", "News Weather")]
    [InlineData("50% Off", "50% Off")]
    [InlineData("50%25%20Off", "50% Off")]
    public void percent_decode_preserves_plus_and_literal_percent(string routeName, string expectedName)
    {
        // Arrange
        var encodedRouteName = routeName;

        // Act
        var name = PodcastRouteNameNormalizer.Normalize(encodedRouteName);

        // Assert
        name.Should().Be(expectedName);
    }
}
