using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// A playlist owned by another channel is useless to the indexer (it keeps only the parent channel's videos), so a
/// parent must not be saved with one. A lookup that cannot be made must refuse the save, not let it through.
/// </summary>
public class YouTubePlaylistOwnershipValidatorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Mock<ITolerantYouTubePlaylistService> _playlists = new();

    private YouTubePlaylistOwnershipValidator Validator() =>
        new(_playlists.Object, NullLogger<YouTubePlaylistOwnershipValidator>.Instance);

    private void PlaylistOwnedBy(string playlistId, string? ownerChannelId, string? ownerTitle = "Synthetic Owner") =>
        _playlists.Setup(p => p.GetPlaylistInfo(
                It.Is<YouTubePlaylistId>(id => id.PlaylistId == playlistId), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetPlaylistInfoResponse("Synthetic Playlist", "", ownerChannelId, ownerTitle));

    [Fact(DisplayName = "A playlist owned by the parent's channel is allowed.")]
    public async Task owned_playlist_matches()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        PlaylistOwnedBy(playlistId, channelId);

        // Act
        var result = await Validator().Validate($" {channelId} ", $" {playlistId} ");

        // Assert
        result.Status.Should().Be(YouTubePlaylistOwnershipStatus.Matches);
        result.IsAllowed.Should().BeTrue();
        result.Code.Should().BeNull();
    }

    [Fact(DisplayName =
        "A playlist owned by another channel is refused with code youTubePlaylistChannelMismatch, naming the owner, and is not retryable.")]
    public async Task other_channel_playlist_mismatches()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var ownerId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        PlaylistOwnedBy(playlistId, ownerId, "Synthetic Other Channel");

        // Act
        var result = await Validator().Validate(channelId, playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistOwnershipStatus.Mismatch);
        result.IsAllowed.Should().BeFalse();
        result.IsRetryable.Should().BeFalse();
        result.Code.Should().Be("youTubePlaylistChannelMismatch");
        result.OwnerChannelId.Should().Be(ownerId);
        result.Message.Should().Contain(ownerId).And.Contain("Synthetic Other Channel").And.Contain(channelId);
    }

    [Fact(DisplayName = "A playlist YouTube does not return (deleted or private) is refused with code youTubePlaylistNotFound.")]
    public async Task missing_playlist_not_found()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        _playlists.Setup(p => p.GetPlaylistInfo(It.IsAny<YouTubePlaylistId>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new YouTubePlaylistNotFoundException(playlistId));

        // Act
        var result = await Validator().Validate(_fixture.CreateYouTubeChannelId(), playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistOwnershipStatus.PlaylistNotFound);
        result.IsAllowed.Should().BeFalse();
        result.IsRetryable.Should().BeFalse();
        result.Code.Should().Be("youTubePlaylistNotFound");
    }

    [Fact(DisplayName =
        "When the YouTube lookup fails (e.g. every api-key out of quota) the save is refused as retryable, not allowed unchecked.")]
    public async Task failed_lookup_is_refused_retryable()
    {
        // Arrange
        _playlists.Setup(p => p.GetPlaylistInfo(It.IsAny<YouTubePlaylistId>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new InvalidOperationException("Unable to obtain playlist-info"));

        // Act
        var result = await Validator().Validate(_fixture.CreateYouTubeChannelId(), _fixture.CreateYouTubePlaylistId());

        // Assert
        result.Status.Should().Be(YouTubePlaylistOwnershipStatus.LookupFailed);
        result.IsAllowed.Should().BeFalse();
        result.IsRetryable.Should().BeTrue();
        result.Code.Should().Be("youTubePlaylistOwnerLookupFailed");
    }

    [Fact(DisplayName = "A response without an owner channel cannot prove ownership, so it is refused as retryable.")]
    public async Task response_without_owner_is_lookup_failure()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        PlaylistOwnedBy(playlistId, null);

        // Act
        var result = await Validator().Validate(_fixture.CreateYouTubeChannelId(), playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistOwnershipStatus.LookupFailed);
    }

    [Theory(DisplayName = "With no playlist, or no channel to compare it with, there is nothing to check and YouTube is not called.")]
    [InlineData(null, "PLsynthetic")]
    [InlineData(" ", "PLsynthetic")]
    [InlineData("UCsynthetic", null)]
    [InlineData("UCsynthetic", "")]
    public async Task blank_side_is_not_applicable(string? channelId, string? playlistId)
    {
        // Arrange
        var validator = Validator();

        // Act
        var result = await validator.Validate(channelId, playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistOwnershipStatus.NotApplicable);
        result.IsAllowed.Should().BeTrue();
        _playlists.VerifyNoOtherCalls();
    }
}
