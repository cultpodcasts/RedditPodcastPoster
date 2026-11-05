using CatalogueMigrate;
using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace CatalogueMigrate.Tests.BusinessRules;

public class CatalogueMigrateServiceIdsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Mock<IYouTubePlaylistValidator> _validator = new();

    [Fact(DisplayName =
        "Service-id guard: when the destination parent document does not carry the podcast's YouTube channel, Spotify " +
        "and Apple ids, then each one is reported dropped, because the writer must refuse a move that loses them.")]
    public void ids_missing_from_the_parent_are_dropped()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.AppleId = _fixture.CreateAppleId();
        });
        var parent = new { name = podcast.Name };

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, parent);

        // Assert
        dropped.Should().BeEquivalentTo("youTubeChannelId", "spotifyId", "appleId");
    }

    [Fact(DisplayName =
        "Service-id guard: ids carried anywhere in the parent document (e.g. under indexing) are not dropped, because " +
        "the guard checks what the writer stores, whatever its shape.")]
    public void ids_nested_in_the_parent_are_kept()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.AppleId = _fixture.CreateAppleId();
        });
        var parent = new
        {
            name = podcast.Name,
            indexing = new { youTubeChannelId = podcast.YouTubeChannelId, appleId = podcast.AppleId }
        };

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, parent);

        // Assert
        dropped.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Service-id guard: a podcast with no service ids drops nothing, because there is nothing to keep.")]
    public void podcast_without_ids_drops_nothing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = null);

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, new { name = podcast.Name });

        // Assert
        dropped.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Service-id guard: the id must match, not just the member name, because a parent carrying a different channel " +
        "id still loses the podcast's own.")]
    public void a_different_value_counts_as_dropped()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = _fixture.CreateYouTubeChannelId());
        var parent = new { indexing = new { youTubeChannelId = _fixture.CreateYouTubeChannelId() } };

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, parent);

        // Assert
        dropped.Should().Equal("youTubeChannelId");
    }

    [Fact(DisplayName =
        "YouTube playlist ownership: a podcast whose channel does not own its playlist is refused with " +
        "youTubePlaylistChannelMismatch before CatalogueMigrate copies the pair (#1178).")]
    public async Task mismatched_channel_playlist_pair_is_refused()
    {
        // Arrange
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var podcastChannelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = podcastChannelId;
            p.YouTubePlaylistId = playlistId;
        });
        _validator.Setup(v => v.Validate(playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.Valid, playlistId, ownerChannelId));

        // Act
        var refused = await CatalogueMigrateServiceIds.YouTubePlaylistOwnershipRefusal(
            podcast, _validator.Object, CancellationToken.None);
        var block = CatalogueMigrateServiceIds.OwnershipBlockReason(refused!);

        // Assert
        refused.Should().NotBeNull();
        refused!.Code.Should().Be(YouTubePlaylistValidation.ChannelMismatchCode);
        block.Should().StartWith(CatalogueMigratePlanner.YouTubePlaylistOwnershipRefusedPrefix);
        block.Should().Contain(YouTubePlaylistValidation.ChannelMismatchCode);
    }

    [Fact(DisplayName =
        "YouTube playlist ownership: a channel-only podcast (no playlist) is not checked, because there is no pair.")]
    public async Task channel_only_podcast_is_not_checked()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = string.Empty;
        });

        // Act
        var refused = await CatalogueMigrateServiceIds.YouTubePlaylistOwnershipRefusal(
            podcast, _validator.Object, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }
}
