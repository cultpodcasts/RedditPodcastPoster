using Api.Extensions;
using Api.Models;
using AutoFixture;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Extensions;

public class PodcastEpisodeRequestExtensionsTests
{
    private readonly Fixture _fixture = new();

    [Theory(DisplayName =
        "Episode resolver mapping percent-decodes a podcast name without treating plus as space, because form-urlencoded UrlDecode would look up a different show.")]
    [InlineData("News+Weather", "News+Weather")]
    [InlineData("News%2BWeather", "News+Weather")]
    [InlineData("News%252BWeather", "News+Weather")]
    public void plus_in_podcast_name_is_preserved(string routeName, string expectedName)
    {
        // Arrange
        var episodeId = _fixture.Create<Guid>();
        var wrapper = new PodcastEpisodeRequestWrapper(routeName, episodeId);

        // Act
        var request = wrapper.ToPodcastEpisodeResolverRequest();

        // Assert
        request.PodcastName.Should().Be(expectedName);
        request.EpisodeId.Should().Be(episodeId);
        request.PodcastId.Should().BeNull();
    }
}
