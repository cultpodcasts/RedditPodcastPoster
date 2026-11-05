using FluentAssertions;
using Moq;
using Api.Services.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace FunctionHost.Tests.Api.Services.YouTube;

/// <summary>
/// Edits check the playlist only when the request sets it to a new value, so unrelated edits to a parent with existing
/// (pre-guard) bad data are not blocked, and no YouTube quota is spent on them. Playlist edits ignore the owner's
/// channel id. Channel edits (when a playlist remains after the request) require the requested channel to own that
/// post-request playlist.
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
        var refused = await guard.CheckChannelAgainstPlaylist(
            Playlist, null, OwnerChannel, requestedChannel, CancellationToken.None);

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
        var refused = await guard.CheckChannelAgainstPlaylist(
            null, null, null, OtherChannel, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Theory(DisplayName =
        "Clearing the playlist in the same request as setting a channel skips the ownership check, because post-apply the channel is the sole YouTube source.")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task clear_playlist_with_channel_skips_check(string requestedPlaylist)
    {
        // Arrange
        var guard = Guard();

        // Act
        var refused = await guard.CheckChannelAgainstPlaylist(
            Playlist, requestedPlaylist, null, OtherChannel, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "A new channel is checked against a playlist replaced in the same request, not the stored playlist.")]
    public async Task channel_checked_against_requested_playlist()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid, OwnerChannel);

        // Act
        var refused = await Guard().CheckChannelAgainstPlaylist(
            Playlist, OtherPlaylist, null, OwnerChannel, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.Verify(v => v.Validate(OtherPlaylist, It.IsAny<CancellationToken>()), Times.Once);
        _validator.Verify(v => v.Validate(Playlist, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A new channel that owns the stored playlist is allowed.")]
    public async Task channel_owning_playlist_is_allowed()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid, OwnerChannel);

        // Act
        var refused = await Guard().CheckChannelAgainstPlaylist(
            Playlist, null, null, $" {OwnerChannel} ", CancellationToken.None);

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
        var refused = await Guard().CheckChannelAgainstPlaylist(
            Playlist, null, null, OtherChannel, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Status.Should().Be(YouTubePlaylistValidationStatus.ChannelMismatch);
        refused.Code.Should().Be(YouTubePlaylistValidation.ChannelMismatchCode);
        refused.OwnerChannelId.Should().Be(OwnerChannel);
        refused.Message.Should().Contain(Playlist);
        refused.Message.Should().Contain(OwnerChannel);
    }

    [Fact(DisplayName =
        "CheckChannelOwnsPlaylist refuses a stored channel/playlist pair the channel does not own, so kind transfer " +
        "cannot copy a mismatched pair (#1178).")]
    public async Task stored_pair_mismatch_is_refused()
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid, OwnerChannel);

        // Act
        var refused = await Guard().CheckChannelOwnsPlaylist(OtherChannel, Playlist, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Code.Should().Be(YouTubePlaylistValidation.ChannelMismatchCode);
        _validator.Verify(v => v.Validate(Playlist, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "CheckChannelOwnsPlaylist skips a channel-only parent with no playlist, because there is no pair to check.")]
    public async Task stored_channel_without_playlist_is_not_checked()
    {
        // Act
        var refused = await Guard().CheckChannelOwnsPlaylist(OtherChannel, null, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Theory(DisplayName =
        "A Valid playlist lookup with a blank owner channel is refused as youTubePlaylistLookupFailed, so ownership does not fail-open.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task valid_playlist_with_blank_owner_is_refused(string? ownerChannelId)
    {
        // Arrange
        ValidatorReturns(YouTubePlaylistValidationStatus.Valid, ownerChannelId);

        // Act
        var refused = await Guard().CheckChannelAgainstPlaylist(
            Playlist, null, null, OtherChannel, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Status.Should().Be(YouTubePlaylistValidationStatus.LookupFailed);
        refused.Code.Should().Be(YouTubePlaylistValidation.LookupFailedCode);
        refused.IsRetryable.Should().BeTrue();
        refused.IsAllowed.Should().BeFalse();
    }

    private const string OwnerChannel = "UCsyntheticownerchannel001";
    private const string OtherChannel = "UCsyntheticotherchannel002";
}
