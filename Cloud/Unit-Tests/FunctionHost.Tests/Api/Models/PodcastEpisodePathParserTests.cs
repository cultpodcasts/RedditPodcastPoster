using Api.Models;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastEpisodePathParserTests
{
    public static TheoryData<string> PodcastNamesWithSpecialCharacters() => new()
    {
        "Was I In A Cult?",
        "True Crime Show w/ Guest Host",
        "Cult? Show w/ Nested Slash",
        "A/B Testing Podcast?"
    };

    [Theory(DisplayName =
        "Trailing episode guid after a podcast name that contains ? and/or /: path splits into full name + episode id, because catch-all paths keep special characters in the name segment.")]
    [MemberData(nameof(PodcastNamesWithSpecialCharacters))]
    public void splits_special_character_podcast_name_and_trailing_episode_id(string podcastName)
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var path = $"{podcastName}/{episodeId:D}";

        // Act
        var ok = PodcastEpisodePathParser.TrySplitTrailingEpisodeId(
            path, out var parsedName, out var parsedEpisodeId);

        // Assert
        ok.Should().BeTrue();
        parsedName.Should().Be(podcastName);
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

    [Fact(DisplayName =
        "Question-mark podcast name without trailing episode guid: split fails so single-segment name lookup can run.")]
    public void rejects_question_mark_name_without_trailing_guid()
    {
        // Arrange
        const string path = "Was I In A Cult?";

        // Act
        var ok = PodcastEpisodePathParser.TrySplitTrailingEpisodeId(
            path, out var podcastName, out var parsedEpisodeId);

        // Assert
        ok.Should().BeFalse();
        podcastName.Should().BeEmpty();
        parsedEpisodeId.Should().Be(Guid.Empty);
    }
}
