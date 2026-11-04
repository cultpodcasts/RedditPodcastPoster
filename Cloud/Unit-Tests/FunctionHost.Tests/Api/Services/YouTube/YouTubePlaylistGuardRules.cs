using FluentAssertions;
using Moq;
using Api.Services.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace FunctionHost.Tests.Api.Services.YouTube;

/// <summary>
/// Edits check the playlist only when the request sets it to a new value, so unrelated edits to a parent with existing
/// (pre-guard) bad data are not blocked, and no YouTube quota is spent on them. The playlist's owner channel is never
/// compared with the parent's channel id.
/// </summary>
public class YouTubePlaylistGuardRules
{
    private const string Playlist = "PLsyntheticplaylist0001";
    private const string OtherPlaylist = "PLsyntheticplaylist0002";

    private readonly Mock<IYouTubePlaylistValidator> _validator = new();

    private YouTubePlaylistGuard Guard() => new(_validator.Object);

    private void ValidatorReturns(YouTubePlaylistValidationStatus status, string? ownerChannelId = null) =>
        _validator.Setup(v => v.Validate(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? p, CancellationToken _) => new YouTubePlaylistValidation(status, p, ownerChannelId));

    [Theory(DisplayName = "An edit that leaves the playlist as it is (absent, or re-sent unchanged) is not checked.")]
    [InlineData(null)]
    [InlineData(Playlist)]
    [InlineData(" " + Playlist + " ")]
    public async Task unchanged_playlist_is_not_checked(string? requestedPlaylist)
    {
        // Arrange
        var guard = Guard();

        // Act
        var refused = await guard.Check(Playlist, requestedPlaylist, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Theory(DisplayName = "Clearing the playlist is allowed without a YouTube call.")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task clearing_playlist_is_not_checked(string requestedPlaylist)
    {
        // Act
        var refused = await Guard().Check(Playlist, requestedPlaylist, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "A new playlist owned by any channel is allowed, because with a playlist the playlist alone is the parent's YouTube source.")]
    public async Task new_playlist_owned_by_another_channel_is_allowed()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid, "UCsyntheticotherowner001");

        // Act
        var refused = await Guard().Check(Playlist, $" {OtherPlaylist} ", CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.Verify(v => v.Validate(OtherPlaylist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "A playlist set on a parent that had none is checked.")]
    public async Task first_playlist_is_checked()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid);

        // Act
        var refused = await Guard().Check(null, Playlist, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.Verify(v => v.Validate(Playlist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = "A new playlist that is malformed, missing or could not be checked is refused with its code.")]
    [InlineData(YouTubePlaylistValidationStatus.InvalidPlaylistId, YouTubePlaylistValidation.InvalidIdCode, false)]
    [InlineData(YouTubePlaylistValidationStatus.PlaylistNotFound, YouTubePlaylistValidation.NotFoundCode, false)]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed, YouTubePlaylistValidation.LookupFailedCode, true)]
    public async Task bad_playlist_is_refused(YouTubePlaylistValidationStatus status, string code, bool retryable)
    {
        // Arrange
        ValidatorReturns(status);

        // Act
        var refused = await Guard().Check(Playlist, OtherPlaylist, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Code.Should().Be(code);
        refused.IsRetryable.Should().Be(retryable);
        refused.Message.Should().Contain(OtherPlaylist);
    }

    [Theory(DisplayName =
        "A channel edit that leaves the channel as it is (absent, cleared, or re-sent unchanged) is not checked against the playlist.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(OwnerChannel)]
    [InlineData(" " + OwnerChannel + " ")]
    public async Task unchanged_or_cleared_channel_is_not_checked(string? requestedChannel)
    {
        // Arrange
        var guard = Guard();

        // Act
        var refused = await guard.CheckChannelAgainstPlaylist(Playlist, OwnerChannel, requestedChannel, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "A channel set on a parent with no playlist is allowed without a YouTube call, because the channel is the YouTube source on its own.")]
    public async Task channel_without_playlist_is_not_checked()
    {
        // Arrange
        var guard = Guard();

        // Act
        var refused = await guard.CheckChannelAgainstPlaylist(null, null, OtherChannel, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "A new channel that owns the stored playlist is allowed.")]
    public async Task channel_owning_playlist_is_allowed()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid, OwnerChannel);

        // Act
        var refused = await Guard().CheckChannelAgainstPlaylist(Playlist, null, $" {OwnerChannel} ", CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.Verify(v => v.Validate(Playlist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A new channel that does not own the stored playlist is refused with youTubePlaylistChannelMismatch.")]
    public async Task channel_not_owning_playlist_is_refused()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid, OwnerChannel);

        // Act
        var refused = await Guard().CheckChannelAgainstPlaylist(Playlist, null, OtherChannel, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Status.Should().Be(YouTubePlaylistValidationStatus.ChannelMismatch);
        refused.Code.Should().Be(YouTubePlaylistValidation.ChannelMismatchCode);
        refused.OwnerChannelId.Should().Be(OwnerChannel);
        refused.Message.Should().Contain(Playlist);
        refused.Message.Should().Contain(OwnerChannel);
    }

    private const string OwnerChannel = "UCsyntheticownerchannel001";
    private const string OtherChannel = "UCsyntheticotherchannel002";
}
