using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastChangeApplierYouTubeIndexingHintsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PodcastChangeApplier _applier = new(NullLogger<PodcastChangeApplier>.Instance);

    [Fact(DisplayName =
        "Podcast GET maps youTubeIndexingHints read-only from the domain, so curators can see the last full walk without editing indexer state.")]
    public void get_exposes_youtube_indexing_hints()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var videoId = _fixture.CreateYouTubeId();
        var lastWalk = DomainTestFixture.UtcDaysAgo(2);
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubePlaylistId = playlistId;
        podcast.YouTubeIndexingHints = new YouTubeIndexingHints
        {
            PlaylistId = playlistId,
            LastFullWalk = lastWalk,
            PlaylistItemCount = 120,
            UploadsCheckedThrough = lastWalk,
            UploadsCheckedThroughVideoId = videoId
        };

        // Act
        var dto = podcast.ToDto();

        // Assert
        dto.YouTubeIndexingHints.Should().NotBeNull();
        dto.YouTubeIndexingHints!.PlaylistId.Should().Be(playlistId);
        dto.YouTubeIndexingHints.LastFullWalk.Should().Be(lastWalk);
        dto.YouTubeIndexingHints.PlaylistItemCount.Should().Be(120);
        dto.YouTubeIndexingHints.UploadsCheckedThroughVideoId.Should().Be(videoId);
    }

    [Fact(DisplayName =
        "Podcast PATCH clearYouTubeIndexingHints true removes only youTubeIndexingHints so the next indexer pass does a full playlist walk.")]
    public void clear_youtube_indexing_hints_removes_hints_only()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        podcast.YouTubeIndexingHints = new YouTubeIndexingHints
        {
            PlaylistId = podcast.YouTubePlaylistId,
            LastFullWalk = DomainTestFixture.UtcDaysAgo(1),
            PlaylistItemCount = 50
        };
        var request = new PodcastChangeRequest { ClearYouTubeIndexingHints = true };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.YouTubeIndexingHints.Should().BeNull();
        podcast.YouTubePlaylistId.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName =
        "Podcast PATCH omitting clearYouTubeIndexingHints leaves existing hints alone, because save must not wipe indexer state.")]
    public void omit_clear_leaves_hints()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var hints = new YouTubeIndexingHints
        {
            PlaylistId = _fixture.CreateYouTubePlaylistId(),
            PlaylistItemCount = 10
        };
        podcast.YouTubeIndexingHints = hints;
        var request = new PodcastChangeRequest { IndexAllEpisodes = true };

        // Act
        _applier.Apply(podcast, request);

        // Assert
        podcast.YouTubeIndexingHints.Should().BeSameAs(hints);
    }
}
