using FluentAssertions;
using Api.Services.YouTube;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace FunctionHost.Tests.Api.Services.YouTube;

/// <summary>
/// Save-time derive: Valid playlist Check already has OwnerChannelId; persist it only when the parent channel is blank.
/// </summary>
public class YouTubePlaylistOwnerChannelRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When playlist Check is Valid with an owner and the parent channel is blank, the owner is returned for persistence.")]
    public void blank_channel_derives_owner()
    {
        // Arrange
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.Valid, playlistId, ownerChannelId);

        // Act
        var derived = YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(check, currentChannelId: null);

        // Assert
        derived.Should().Be(ownerChannelId);
    }

    [Theory(DisplayName =
        "When the parent channel is blank (null, empty, or whitespace), a Valid playlist owner is derived.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void blankish_channel_derives_owner(string? currentChannelId)
    {
        // Arrange
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.Valid,
            _fixture.CreateYouTubePlaylistId(),
            ownerChannelId);

        // Act
        var derived = YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(check, currentChannelId);

        // Assert
        derived.Should().Be(ownerChannelId);
    }

    [Fact(DisplayName =
        "When the parent already has a non-blank channel, the playlist owner is not returned, so an intentional channel is not overwritten.")]
    public void non_blank_channel_is_not_overwritten()
    {
        // Arrange
        var storedChannelId = _fixture.CreateYouTubeChannelId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.Valid,
            _fixture.CreateYouTubePlaylistId(),
            ownerChannelId);

        // Act
        var derived = YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(check, storedChannelId);

        // Assert
        derived.Should().BeNull();
    }

    [Fact(DisplayName =
        "When playlist Check is null (no playlist change / no lookup), nothing is derived.")]
    public void null_check_derives_nothing()
    {
        // Act
        var derived = YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(null, currentChannelId: null);

        // Assert
        derived.Should().BeNull();
    }

    [Theory(DisplayName =
        "When playlist Check is not Valid, or Valid without an owner, nothing is derived.")]
    [InlineData(YouTubePlaylistValidationStatus.PlaylistNotFound)]
    [InlineData(YouTubePlaylistValidationStatus.InvalidPlaylistId)]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed)]
    [InlineData(YouTubePlaylistValidationStatus.ChannelMismatch)]
    public void non_valid_check_derives_nothing(YouTubePlaylistValidationStatus status)
    {
        // Arrange
        var check = new YouTubePlaylistValidation(status, _fixture.CreateYouTubePlaylistId(), _fixture.CreateYouTubeChannelId());

        // Act
        var derived = YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(check, currentChannelId: null);

        // Assert
        derived.Should().BeNull();
    }

    [Theory(DisplayName =
        "When playlist Check is Valid but OwnerChannelId is blank, nothing is derived.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void valid_without_owner_derives_nothing(string? ownerChannelId)
    {
        // Arrange
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.Valid,
            _fixture.CreateYouTubePlaylistId(),
            ownerChannelId);

        // Act
        var derived = YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(check, currentChannelId: null);

        // Assert
        derived.Should().BeNull();
    }
}
