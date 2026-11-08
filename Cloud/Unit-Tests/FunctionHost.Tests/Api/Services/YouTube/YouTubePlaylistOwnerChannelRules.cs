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

    [Fact(DisplayName =
        "OwnerFromValidCheck returns the trimmed owner from a Valid Check so youTubePlaylistOwnerChannelId can be " +
        "persisted for the uploads-head skip (#1183).")]
    public void owner_from_valid_check_returns_trimmed_owner()
    {
        // Arrange
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.Valid,
            _fixture.CreateYouTubePlaylistId(),
            $"  {ownerChannelId}  ");

        // Act
        var owner = YouTubePlaylistOwnerChannel.OwnerFromValidCheck(check);

        // Assert
        owner.Should().Be(ownerChannelId);
    }

    [Fact(DisplayName =
        "OwnerFromValidCheck returns null when Check is not Valid, so a failed playlist lookup does not clear or invent " +
        "a stored playlist owner.")]
    public void owner_from_non_valid_check_is_null()
    {
        // Arrange
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.PlaylistNotFound,
            _fixture.CreateYouTubePlaylistId(),
            _fixture.CreateYouTubeChannelId());

        // Act
        var owner = YouTubePlaylistOwnerChannel.OwnerFromValidCheck(check);

        // Assert
        owner.Should().BeNull();
    }

    [Fact(DisplayName =
        "PersistFromCheck clears the stored playlist owner when the effective playlist is blank.")]
    public void persist_from_check_clears_when_playlist_blank()
    {
        // Arrange
        string? storedOwner = _fixture.CreateYouTubeChannelId();
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.Valid,
            _fixture.CreateYouTubePlaylistId(),
            _fixture.CreateYouTubeChannelId());

        // Act
        YouTubePlaylistOwnerChannel.PersistFromCheck(
            effectivePlaylistId: null,
            check,
            () => storedOwner = null,
            owner => storedOwner = owner);

        // Assert
        storedOwner.Should().BeNull();
    }

    [Fact(DisplayName =
        "PersistFromCheck sets youTubePlaylistOwnerChannelId from a Valid Check when a non-blank playlist remains.")]
    public void persist_from_check_sets_owner_from_valid_check()
    {
        // Arrange
        string? storedOwner = null;
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var check = new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.Valid, playlistId, ownerChannelId);

        // Act
        YouTubePlaylistOwnerChannel.PersistFromCheck(
            playlistId,
            check,
            () => storedOwner = null,
            owner => storedOwner = owner);

        // Assert
        storedOwner.Should().Be(ownerChannelId);
    }

    [Fact(DisplayName =
        "PersistFromCheck leaves a stored playlist owner unchanged when Check is null (unchanged playlist that already had an owner).")]
    public void persist_from_check_leaves_owner_when_check_null()
    {
        // Arrange
        var existingOwner = _fixture.CreateYouTubeChannelId();
        string? storedOwner = existingOwner;

        // Act
        YouTubePlaylistOwnerChannel.PersistFromCheck(
            _fixture.CreateYouTubePlaylistId(),
            playlistCheck: null,
            () => storedOwner = null,
            owner => storedOwner = owner);

        // Assert
        storedOwner.Should().Be(existingOwner);
    }
}
