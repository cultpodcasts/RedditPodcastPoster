using FluentAssertions;
using FluentAssertions.Execution;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.TestSupport.Assertions;

public static class EpisodeExpectationAssertions
{
    public static void ShouldMatchExpectation(this Episode episode, EpisodeExpectation expected)
    {
        using var scope = new AssertionScope();

        var actual = EpisodeExpectation.From(episode);
        actual.Spotify.Should().BeEquivalentTo(expected.Spotify);
        actual.Apple.Should().BeEquivalentTo(expected.Apple);
        actual.YouTube.Should().BeEquivalentTo(expected.YouTube);
        actual.Release.Should().Be(expected.Release);
        actual.Description.Should().Be(expected.Description);
        actual.Ignored.Should().Be(expected.Ignored);
        actual.Removed.Should().Be(expected.Removed);
    }
}
