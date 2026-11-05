using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using PodcastKindTransferResponse = Api.Dtos.PodcastKindTransferResponse;
using ApiErrorResponse = Api.Dtos.ApiErrorResponse;
using Api.Handlers;
using Api.Handlers.Podcasts;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class PostPodcastKindTransferHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private readonly PodcastKindTransferCommand _command;
    private PodcastKindTransferResult _result;

    public PostPodcastKindTransferHandlerTests()
    {
        var podcastId = _fixture.CreateGuid();
        _command = new PodcastKindTransferCommand(
            podcastId,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation });
        _result = new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted, podcastId, CatalogueParentKind.NewsOrganisation, 3);
        _mocker.GetMock<IPodcastKindTransferService>()
            .Setup(s => s.TransferAsync(It.IsAny<Guid>(), It.IsAny<PodcastKindTransferRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        _ctx = _mocker.GetMock<IHandlerContext>();
    }

    private void VerifyLogged(LogLevel level, Times times) =>
        _mocker.GetMock<ILogger<PostPodcastKindTransferHandler>>().Verify(l => l.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    [Fact(DisplayName =
        "A transfer refused because another transfer of the podcast is running is a 409 whose body says " +
        "transferInProgress, so the site can tell it apart from an id clash.")]
    public async Task already_running_is_conflict_with_in_progress_flag()
    {
        // Arrange
        _result = new PodcastKindTransferResult(
            PodcastKindTransferStatus.AlreadyRunning, _command.PodcastId, CatalogueParentKind.NewsOrganisation);
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<PodcastKindTransferResponse>(r => r.TransferInProgress && r.ParentId == _command.PodcastId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A transfer that kept the podcast because episodes kept arriving is a 409 whose body says retryRequired " +
        "(and not transferInProgress), so the site asks the curator to run the transfer again.")]
    public async Task incomplete_is_conflict_with_retry_required_flag()
    {
        // Arrange
        _result = new PodcastKindTransferResult(
            PodcastKindTransferStatus.Incomplete, _command.PodcastId, CatalogueParentKind.NewsOrganisation, 5);
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<PodcastKindTransferResponse>(r => r.RetryRequired && !r.TransferInProgress &&
                                                    r.ParentId == _command.PodcastId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When the caller disconnects and writing the 202 reply is cancelled after a successful transfer, the " +
        "handler logs it as information (not an error) and returns the 202 status, because the transfer is stored.")]
    public async Task cancelled_reply_after_success_is_information()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        _ctx.Setup(x => x.Accepted(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                caller.Cancel();
                return Task.FromException<Microsoft.Azure.Functions.Worker.Http.HttpResponseData>(
                    new TaskCanceledException());
            });
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        var act = () => sut.Handle(_ctx.Object, _command, caller.Token);

        // Assert
        await act.Should().NotThrowAsync();
        _ctx.Verify(x => x.Status(HttpStatusCode.Accepted), Times.Once);
        VerifyLogged(LogLevel.Information, Times.Once());
        VerifyLogged(LogLevel.Error, Times.Never());
    }

    [Fact(DisplayName =
        "When the caller has already gone by the time the transfer finishes, the handler does not try to write a " +
        "body and logs the stored outcome as information.")]
    public async Task caller_gone_before_reply_skips_body()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, caller.Token);

        // Assert
        _ctx.Verify(x => x.Accepted(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _ctx.Verify(x => x.Status(HttpStatusCode.Accepted), Times.Once);
        VerifyLogged(LogLevel.Information, Times.Once());
    }

    [Fact(DisplayName = "A successful transfer with a connected caller is a 202 with the transfer body.")]
    public async Task success_is_accepted_with_body()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostPodcastKindTransferHandler>();

        // Act
        await sut.Handle(_ctx.Object, _command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Accepted(
            It.Is<PodcastKindTransferResponse>(r => r.PlayableCount == 3 && !r.TransferInProgress),
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
