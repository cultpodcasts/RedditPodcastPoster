using FluentAssertions;
using Moq;
using Api.Services.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace FunctionHost.Tests.Api.Services.YouTube;

/// <summary>
/// Edits check playlist ownership only when the playlist or channel changes, so unrelated edits to a parent with
/// existing (pre-guard) bad data are not blocked, and no YouTube quota is spent on them.
/// </summary>
public class YouTubePlaylistOwnershipGuardRules
{
    private const string Channel = "UCsyntheticchannel000001";
    private const string OtherChannel = "UCsyntheticchannel000002";
    private const string Playlist = "PLsyntheticplaylist0001";
    private const string OtherPlaylist = "PLsyntheticplaylist0002";

    private readonly Mock<IYouTubePlaylistOwnershipValidator> _validator = new();

    private YouTubePlaylistOwnershipGuard Guard() => new(_validator.Object);

    private void ValidatorReturns(YouTubePlaylistOwnershipStatus status) =>
        _validator.Setup(v => v.Validate(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? c, string? p, CancellationToken _) => new YouTubePlaylistOwnership(status, c, p));

    [Theory(DisplayName = "An edit that leaves the playlist and channel as they are is not checked.")]
    [InlineData(null, null)]
    [InlineData(Channel, Playlist)]
    [InlineData(" " + Channel + " ", Playlist + " ")]
    public async Task unchanged_pair_is_not_checked(string? requestedChannel, string? requestedPlaylist)
    {
        // Arrange
        var guard = Guard();

        // Act
        var refused = await guard.Check(Channel, Playlist, requestedChannel, requestedPlaylist, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "A new playlist is checked against the stored channel, and refused when another channel owns it.")]
    public async Task new_playlist_checked_against_stored_channel()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistOwnershipStatus.Mismatch);

        // Act
        var refused = await Guard().Check(Channel, Playlist, null, $" {OtherPlaylist} ", CancellationToken.None);

        // Assert
        refused!.Code.Should().Be(YouTubePlaylistOwnership.MismatchCode);
        _validator.Verify(v => v.Validate(Channel, OtherPlaylist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "A new channel is checked against the stored playlist, because changing either side can break the pair.")]
    public async Task new_channel_checked_against_stored_playlist()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistOwnershipStatus.Matches);

        // Act
        var refused = await Guard().Check(Channel, Playlist, OtherChannel, null, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.Verify(v => v.Validate(OtherChannel, Playlist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Clearing the playlist is allowed (nothing to check).")]
    public async Task clearing_playlist_is_allowed()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistOwnershipStatus.NotApplicable);

        // Act
        var refused = await Guard().Check(Channel, Playlist, null, "", CancellationToken.None);

        // Assert
        refused.Should().BeNull();
    }

    [Fact(DisplayName = "A failed lookup refuses the edit as retryable.")]
    public async Task failed_lookup_refuses_retryable()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistOwnershipStatus.LookupFailed);

        // Act
        var refused = await Guard().Check(Channel, null, null, Playlist, CancellationToken.None);

        // Assert
        refused!.IsRetryable.Should().BeTrue();
    }
}
