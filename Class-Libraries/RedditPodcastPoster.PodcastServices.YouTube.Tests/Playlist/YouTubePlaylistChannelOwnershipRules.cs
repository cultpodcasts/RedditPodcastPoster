using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// Copy paths (kind transfer) must not propagate a youTubeChannelId / youTubePlaylistId pair the
/// channel does not own (#1178).
/// </summary>
public class YouTubePlaylistChannelOwnershipRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Mock<IYouTubePlaylistValidator> _validator = new();

    [Theory(DisplayName =
        "A channel-only or playlist-only parent is not ownership-checked, because there is no pair to copy.")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task incomplete_pair_is_not_checked(bool hasChannel, bool hasPlaylist)
    {
        // Arrange
        var channelId = hasChannel ? _fixture.CreateYouTubeChannelId() : null;
        var playlistId = hasPlaylist ? _fixture.CreateYouTubePlaylistId() : null;

        // Act
        var refused = await YouTubePlaylistChannelOwnership.Check(
            _validator.Object, channelId, playlistId, CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "A channel that owns the playlist may be copied with the playlist.")]
    public async Task owning_channel_is_allowed()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        _validator.Setup(v => v.Validate(playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.Valid, playlistId, channelId));

        // Act
        var refused = await YouTubePlaylistChannelOwnership.Check(
            _validator.Object, $" {channelId} ", $" {playlistId} ", CancellationToken.None);

        // Assert
        refused.Should().BeNull();
        _validator.Verify(v => v.Validate(playlistId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A channel that does not own the playlist is refused with youTubePlaylistChannelMismatch.")]
    public async Task mismatched_channel_is_refused()
    {
        // Arrange
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var otherChannelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        _validator.Setup(v => v.Validate(playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.Valid, playlistId, ownerChannelId));

        // Act
        var refused = await YouTubePlaylistChannelOwnership.Check(
            _validator.Object, otherChannelId, playlistId, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Status.Should().Be(YouTubePlaylistValidationStatus.ChannelMismatch);
        refused.Code.Should().Be(YouTubePlaylistValidation.ChannelMismatchCode);
        refused.OwnerChannelId.Should().Be(ownerChannelId);
    }

    [Theory(DisplayName =
        "A playlist that is malformed, missing, or could not be looked up is refused with its edit-endpoint code.")]
    [InlineData(YouTubePlaylistValidationStatus.InvalidPlaylistId, YouTubePlaylistValidation.InvalidIdCode, false)]
    [InlineData(YouTubePlaylistValidationStatus.PlaylistNotFound, YouTubePlaylistValidation.NotFoundCode, false)]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed, YouTubePlaylistValidation.LookupFailedCode, true)]
    public async Task bad_playlist_is_refused(
        YouTubePlaylistValidationStatus status, string code, bool retryable)
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        _validator.Setup(v => v.Validate(playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(status, playlistId));

        // Act
        var refused = await YouTubePlaylistChannelOwnership.Check(
            _validator.Object, channelId, playlistId, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Code.Should().Be(code);
        refused.IsRetryable.Should().Be(retryable);
    }

    [Theory(DisplayName =
        "A Valid playlist lookup with a blank owner channel is refused as youTubePlaylistLookupFailed, so ownership does not fail-open.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task valid_playlist_with_blank_owner_is_refused(string? ownerChannelId)
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        _validator.Setup(v => v.Validate(playlistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.Valid, playlistId, ownerChannelId));

        // Act
        var refused = await YouTubePlaylistChannelOwnership.Check(
            _validator.Object, channelId, playlistId, CancellationToken.None);

        // Assert
        refused.Should().NotBeNull();
        refused!.Status.Should().Be(YouTubePlaylistValidationStatus.LookupFailed);
        refused.Code.Should().Be(YouTubePlaylistValidation.LookupFailedCode);
        refused.IsRetryable.Should().BeTrue();
    }
}
