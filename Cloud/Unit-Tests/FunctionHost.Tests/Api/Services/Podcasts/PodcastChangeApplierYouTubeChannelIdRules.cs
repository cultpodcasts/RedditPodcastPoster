using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using System.Text.Json;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

/// <summary>
/// Podcast PATCH applies youTubeChannelId (previously accepted on the DTO but ignored by the applier), so a
/// playlist-without-channel podcast can get a matching-identity channel through the curator edit API (#1179).
/// </summary>
public class PodcastChangeApplierYouTubeChannelIdRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PodcastChangeApplier _applier = new(NullLogger<PodcastChangeApplier>.Instance);

    [Fact(DisplayName =
        "Podcast PATCH youTubeChannelId stores the trimmed id on a podcast that had none, because curators must be able to set matching identity for a playlist-without-channel parent.")]
    public void applies_trimmed_youtube_channel_id()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = string.Empty);
        var request = new PodcastChangeRequest { YouTubeChannelId = $"  {channelId} " };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.YouTubeChannelId.Should().Be(channelId);
    }

    [Fact(DisplayName =
        "Podcast PATCH youTubeChannelId \"\" or whitespace clears the stored channel to empty, matching SpotifyId clear semantics on Podcast.")]
    public void empty_or_whitespace_clears_youtube_channel_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = _fixture.CreateYouTubeChannelId());
        var request = new PodcastChangeRequest { YouTubeChannelId = "   " };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.YouTubeChannelId.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Podcast PATCH omitting youTubeChannelId leaves the stored channel unchanged.")]
    public void omit_leaves_youtube_channel_id()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = channelId);
        var request = new PodcastChangeRequest { IndexAllEpisodes = true };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.YouTubeChannelId.Should().Be(channelId);
    }

    [Fact(DisplayName =
        "PATCH youTubeChannelId wire name reaches the podcast model via ToModel so a rename or missed copy fails tests.")]
    public void youtube_channel_id_wire_name()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var json = $$"""{"youTubeChannelId":"{{channelId}}"}""";

        // Act
        var change = JsonSerializer.Deserialize<global::Api.Dtos.PodcastChangeRequest>(json)!.ToModel();

        // Assert
        change.YouTubeChannelId.Should().Be(channelId);
    }
}
