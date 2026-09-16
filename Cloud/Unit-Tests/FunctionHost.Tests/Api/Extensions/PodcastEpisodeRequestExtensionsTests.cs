using Api.Extensions;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Extensions;

public class PodcastEpisodeRequestExtensionsTests
{
    [Fact(DisplayName =
        "Episode resolver mapping keeps '+' in a podcast name, because form-urlencoded UrlDecode would look up a different show.")]
    public void plus_in_podcast_name_is_preserved()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var wrapper = new PodcastEpisodeRequestWrapper("News+Weather", episodeId);

        // Act
        var request = wrapper.ToPodcastEpisodeResolverRequest();

        // Assert
        request.PodcastName.Should().Be("News+Weather");
        request.EpisodeId.Should().Be(episodeId);
        request.PodcastId.Should().BeNull();
    }

    [Fact(DisplayName =
        "Episode resolver mapping percent-decodes '%2B' to '+', because Worker proxy re-encodes plus in the Azure path.")]
    public void encoded_plus_in_podcast_name_decodes_to_plus()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var wrapper = new PodcastEpisodeRequestWrapper("News%2BWeather", episodeId);

        // Act
        var request = wrapper.ToPodcastEpisodeResolverRequest();

        // Assert
        request.PodcastName.Should().Be("News+Weather");
        request.EpisodeId.Should().Be(episodeId);
    }
}
