using Api.Models;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastEpisodePathParserTests
{
    [Fact(DisplayName =
        "Trailing episode guid after a slash-containing podcast name: path splits into name + episode id, because hosts decode %2F into path separators.")]
    public void splits_slash_containing_podcast_name_and_trailing_episode_id()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var path = $"True Crime Show w/ Guest Host/{episodeId:D}";

        // Act
        var ok = PodcastEpisodePathParser.TrySplitTrailingEpisodeId(
            path, out var podcastName, out var parsedEpisodeId);

        // Assert
        ok.Should().BeTrue();
        podcastName.Should().Be("True Crime Show w/ Guest Host");
        parsedEpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "Trailing episode guid after a single-segment podcast name: path splits into name + episode id for the two-segment route shape.")]
    public void splits_single_segment_podcast_name_and_trailing_episode_id()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var path = $"Plain Podcast Name/{episodeId:D}";

        // Act
        var ok = PodcastEpisodePathParser.TrySplitTrailingEpisodeId(
            path, out var podcastName, out var parsedEpisodeId);

        // Assert
        ok.Should().BeTrue();
        podcastName.Should().Be("Plain Podcast Name");
        parsedEpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "Path without a trailing guid: split fails, because the catch-all cannot invent an episode id.")]
    public void rejects_path_without_trailing_guid()
    {
        // Arrange
        const string path = "True Crime Show w/ Guest Host";

        // Act
        var ok = PodcastEpisodePathParser.TrySplitTrailingEpisodeId(
            path, out var podcastName, out var parsedEpisodeId);

        // Assert
        ok.Should().BeFalse();
        podcastName.Should().BeEmpty();
        parsedEpisodeId.Should().Be(Guid.Empty);
    }
}
