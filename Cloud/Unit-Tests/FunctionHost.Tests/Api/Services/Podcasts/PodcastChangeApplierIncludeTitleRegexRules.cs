using Api.Models;
using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastChangeApplierIncludeTitleRegexRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PodcastChangeApplier _applier = new(NullLogger<PodcastChangeApplier>.Instance);

    [Fact(DisplayName =
        "Podcast change: when EpisodeIncludeTitleRegex is a non-empty pattern, then IndexAllEpisodes is cleared, because include-title filtering only runs when Index All is off.")]
    public void non_empty_include_title_regex_clears_index_all_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.IndexAllEpisodes = true;
        podcast.EpisodeIncludeTitleRegex = string.Empty;
        var pattern = _fixture.CreateShortTitle();
        var request = new PodcastChangeRequest { EpisodeIncludeTitleRegex = pattern };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.EpisodeIncludeTitleRegex.Should().Be(pattern);
        podcast.IndexAllEpisodes.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Podcast change: when EpisodeIncludeTitleRegex is cleared to empty, then IndexAllEpisodes is left unchanged, because only a real include pattern overrides Index All.")]
    public void clearing_include_title_regex_does_not_force_index_all_off()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.IndexAllEpisodes = true;
        podcast.EpisodeIncludeTitleRegex = _fixture.CreateShortTitle();
        var request = new PodcastChangeRequest { EpisodeIncludeTitleRegex = string.Empty };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.EpisodeIncludeTitleRegex.Should().BeEmpty();
        podcast.IndexAllEpisodes.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Podcast change: when IndexAllEpisodes is set true, then EpisodeIncludeTitleRegex is cleared, because Index All skips the include-title filter and the two modes must not both stick.")]
    public void enabling_index_all_clears_include_title_regex()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.IndexAllEpisodes = false;
        podcast.EpisodeIncludeTitleRegex = _fixture.CreateShortTitle();
        var request = new PodcastChangeRequest { IndexAllEpisodes = true };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.IndexAllEpisodes.Should().BeTrue();
        podcast.EpisodeIncludeTitleRegex.Should().BeEmpty();
    }
}
