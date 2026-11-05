using Moq;
using Moq.AutoMock;
using PodcastKindTransferEnqueueResponse = Api.Dtos.PodcastKindTransferEnqueueResponse;
using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Podcasts;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;
using PodcastKindTransferRequest = Api.Models.PodcastKindTransferRequest;

namespace FunctionHost.Tests.Api.Handlers;

public class PostPodcastKindTransferHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private readonly PodcastKindTransferCommand _command;
    private PodcastKindTransferEnqueueResult _result;

    public PostPodcastKindTransferHandlerTests()
    {
        var podcastId = _fixture.CreateGuid();
        _command = new PodcastKindTransferCommand(
            podcastId,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation });
        _result = new PodcastKindTransferEnqueueResult(
            PodcastKindTransferEnqueueStatus.Queued, podcastId, CatalogueParentKind.NewsOrganisation,
            _fixture.Create<DateTime>().ToUniversalTime());
        _mocker.GetMock<IPodcastKindTransferEnqueueService>()
            .Setup(s => s.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<PodcastKindTransferRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        _ctx = _mocker.GetMock<IHandlerContext>();
    }

    [Fact(DisplayName =
        "A successful enqueue is a 202 whose body says Queued and points at GET podcast/{id}/kind for progress.")]
    public async Task queued_is_accepted_with_status_path()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Accepted(
            It.Is<PodcastKindTransferEnqueueResponse>(r =>
                r.Status == nameof(PodcastKindTransferEnqueueStatus.Queued) &&
                r.ParentId == _command.PodcastId &&
                r.StatusPath == $"podcast/{_command.PodcastId:D}/kind"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "An enqueue refused because another transfer of the podcast is running is a 409 whose body says " +
        "AlreadyRunning, so the site can poll status instead of retrying the command.")]
    public async Task already_running_is_conflict()
    {
        // Arrange
        _result = new PodcastKindTransferEnqueueResult(
            PodcastKindTransferEnqueueStatus.AlreadyRunning, _command.PodcastId,
            CatalogueParentKind.NewsOrganisation);
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<PodcastKindTransferEnqueueResponse>(r =>
                r.Status == nameof(PodcastKindTransferEnqueueStatus.AlreadyRunning) &&
                r.ParentId == _command.PodcastId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "An enqueue for an unknown podcast is a 404.")]
    public async Task missing_podcast_is_not_found()
    {
        // Arrange
        _result = new PodcastKindTransferEnqueueResult(
            PodcastKindTransferEnqueueStatus.NotFound, _command.PodcastId, CatalogueParentKind.NewsOrganisation);
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.NotFound(
            It.Is<object>(o => o != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "An enqueue with an invalid target kind is a 400.")]
    public async Task invalid_target_is_bad_request()
    {
        // Arrange
        _result = new PodcastKindTransferEnqueueResult(PodcastKindTransferEnqueueStatus.InvalidTarget);
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.BadRequest(
            It.Is<ApiErrorResponse>(e => e != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A kind transfer refused for youTubePlaylistChannelMismatch is a 400 with {error, code}, matching the " +
        "podcast edit contract (#1178).")]
    public async Task channel_mismatch_is_bad_request_with_code()
    {
        // Arrange
        _result = new PodcastKindTransferResult(
            PodcastKindTransferStatus.BadRequest,
            _command.PodcastId,
            CatalogueParentKind.NewsOrganisation,
            Error: "channel does not own playlist",
            Code: YouTubePlaylistValidation.ChannelMismatchCode);
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.BadRequest(
            It.Is<ApiErrorResponse>(r =>
                r.Error == "channel does not own playlist" &&
                r.Code == YouTubePlaylistValidation.ChannelMismatchCode &&
                r.Retryable == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A kind transfer that could not look up the playlist is a 503 with {error, code, retryable:true}.")]
    public async Task lookup_failed_is_service_unavailable_with_retryable()
    {
        // Arrange
        _result = new PodcastKindTransferResult(
            PodcastKindTransferStatus.ServiceUnavailable,
            _command.PodcastId,
            CatalogueParentKind.TvShow,
            Error: "try again",
            Code: YouTubePlaylistValidation.LookupFailedCode);
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Json(
            HttpStatusCode.ServiceUnavailable,
            It.Is<ApiErrorResponse>(r =>
                r.Error == "try again" &&
                r.Code == YouTubePlaylistValidation.LookupFailedCode &&
                r.Retryable == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
