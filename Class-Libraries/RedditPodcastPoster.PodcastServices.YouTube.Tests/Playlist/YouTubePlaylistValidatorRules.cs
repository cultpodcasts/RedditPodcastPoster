using System.Net;
using FluentAssertions;
using Google;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// A parent with a playlist indexes only from it, so a malformed, deleted or private playlist id must not be saved. Any
/// owner channel is accepted. A lookup that cannot be made must refuse the save, not let it through.
/// </summary>
public class YouTubePlaylistValidatorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Mock<ITolerantYouTubePlaylistService> _playlists = new();

    private YouTubePlaylistValidator Validator() =>
        new(_playlists.Object, NullLogger<YouTubePlaylistValidator>.Instance);

    private void PlaylistOwnedBy(string playlistId, string? ownerChannelId, string? ownerTitle = "Synthetic Owner") =>
        _playlists.Setup(p => p.GetPlaylistInfo(
                It.Is<YouTubePlaylistId>(id => id.PlaylistId == playlistId), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetPlaylistInfoResponse("Synthetic Playlist", "", ownerChannelId, ownerTitle));

    [Fact(DisplayName = "An existing playlist is valid, and its owner channel is reported for information.")]
    public async Task existing_playlist_is_valid()
    {
        // Arrange
        var ownerId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        PlaylistOwnedBy(playlistId, ownerId, "Synthetic Owner");

        // Act
        var result = await Validator().Validate($" {playlistId} ");

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.Valid);
        result.IsAllowed.Should().BeTrue();
        result.Code.Should().BeNull();
        result.Message.Should().BeNull();
        result.PlaylistId.Should().Be(playlistId);
        result.OwnerChannelId.Should().Be(ownerId);
        result.OwnerChannelTitle.Should().Be("Synthetic Owner");
    }

    [Fact(DisplayName = "A playlist YouTube does not return (deleted or private) is refused with code youTubePlaylistNotFound.")]
    public async Task missing_playlist_not_found()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        _playlists.Setup(p => p.GetPlaylistInfo(It.IsAny<YouTubePlaylistId>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new YouTubePlaylistNotFoundException(playlistId));

        // Act
        var result = await Validator().Validate(playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.PlaylistNotFound);
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
        var result = await Validator().Validate(_fixture.CreateYouTubePlaylistId());

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.LookupFailed);
        result.IsAllowed.Should().BeFalse();
        result.IsRetryable.Should().BeTrue();
        result.Code.Should().Be("youTubePlaylistLookupFailed");
    }

    [Theory(DisplayName =
        "A YouTube 404 (playlistNotFound) or 400 for the id is a non-retryable youTubePlaylistNotFound, because retrying cannot fix a bad or deleted id.")]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task youtube_not_found_or_bad_request_is_not_found(HttpStatusCode statusCode)
    {
        // Arrange
        _playlists.Setup(p => p.GetPlaylistInfo(It.IsAny<YouTubePlaylistId>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new GoogleApiException("youtube", "playlistNotFound") { HttpStatusCode = statusCode });

        // Act
        var result = await Validator().Validate(_fixture.CreateYouTubePlaylistId());

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.PlaylistNotFound);
        result.IsRetryable.Should().BeFalse();
        result.Code.Should().Be(YouTubePlaylistValidation.NotFoundCode);
    }

    [Theory(DisplayName =
        "A YouTube 5xx or a non-quota 403 stays a retryable lookup failure, because the id may be fine.")]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task youtube_server_error_is_lookup_failure(HttpStatusCode statusCode)
    {
        // Arrange
        _playlists.Setup(p => p.GetPlaylistInfo(It.IsAny<YouTubePlaylistId>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new GoogleApiException("youtube", "backendError") { HttpStatusCode = statusCode });

        // Act
        var result = await Validator().Validate(_fixture.CreateYouTubePlaylistId());

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.LookupFailed);
        result.IsRetryable.Should().BeTrue();
    }

    [Theory(DisplayName =
        "A value that is not a playlist id (stray '@', spaces, too short, a pasted URL) is refused as youTubePlaylistIdInvalid without calling YouTube.")]
    [InlineData("@PLS3XGZxi7cBXo0K8Ts08kgyNwYll5n89O")]
    [InlineData("PL abc def ghi jkl")]
    [InlineData("PLshort")]
    [InlineData("https://www.youtube.com/playlist?list=PLS3XGZxi7cBXo0K8Ts08kgyNwYll5n89O")]
    public async Task malformed_id_is_refused_without_a_call(string playlistId)
    {
        // Arrange
        var validator = Validator();

        // Act
        var result = await validator.Validate(playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.InvalidPlaylistId);
        result.IsAllowed.Should().BeFalse();
        result.IsRetryable.Should().BeFalse();
        result.Code.Should().Be("youTubePlaylistIdInvalid");
        _playlists.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "A pasted playlist URL is refused with a message suggesting the id inside it.")]
    public async Task pasted_url_message_suggests_the_id()
    {
        // Arrange
        var id = _fixture.CreateYouTubePlaylistId();

        // Act
        var result = await Validator().Validate(
            $"https://www.youtube.com/playlist?list={id}&si=abc");

        // Assert
        result.Message.Should().Contain($"Did you mean '{id}'?");
    }

    [Fact(DisplayName = "A response without an owner channel is still valid, because ownership is not checked.")]
    public async Task response_without_owner_is_valid()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        PlaylistOwnedBy(playlistId, null, null);

        // Act
        var result = await Validator().Validate(playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.Valid);
        result.IsAllowed.Should().BeTrue();
    }

    [Theory(DisplayName = "With no playlist there is nothing to check and YouTube is not called.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task blank_playlist_is_not_applicable(string? playlistId)
    {
        // Arrange
        var validator = Validator();

        // Act
        var result = await validator.Validate(playlistId);

        // Assert
        result.Status.Should().Be(YouTubePlaylistValidationStatus.NotApplicable);
        result.IsAllowed.Should().BeTrue();
        _playlists.VerifyNoOtherCalls();
    }
}
