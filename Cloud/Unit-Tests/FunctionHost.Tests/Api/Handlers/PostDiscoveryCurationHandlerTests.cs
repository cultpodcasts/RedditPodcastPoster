using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.Discovery;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class PostDiscoveryCurationHandlerTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private DiscoverySubmissionHoldResult _holdResult = new(DiscoverySubmissionHoldStatus.Failed);
    private DiscoveryCurationSubmitResult _submitResult = new(DiscoveryCurationSubmitStatus.Failed);

    public PostDiscoveryCurationHandlerTests()
    {
        _mocker.GetMock<IDiscoverySubmissionHoldService>()
            .Setup(s => s.HoldAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _holdResult);
        _mocker.GetMock<IDiscoveryCurationSubmitService>()
            .Setup(s => s.SubmitAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _submitResult);
    }

    [Fact(DisplayName =
        "When a curate:supervised caller submits, the submit is held: 202 with the submission id and Pending status, and nothing is applied, because supervised work needs supervisor review first.")]
    public async Task supervised_submit_is_held_not_applied()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var submissionId = _fixture.Create<Guid>();
        _holdResult = new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.Held, submissionId);
        var sut = _mocker.CreateInstance<PostDiscoveryCurationHandler>();

        // Act
        var result = await sut.Handle(Context(subject, "curate", "curate:supervised"), new DiscoverySubmitRequest(),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await ReadJson(result);
        body.GetProperty("submissionId").GetGuid().Should().Be(submissionId);
        body.GetProperty("status").GetString().Should().Be("Pending");
        _mocker.GetMock<IDiscoverySubmissionHoldService>().Verify(
            s => s.HoldAsync(It.IsAny<DiscoverySubmitRequest>(), subject, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IDiscoveryCurationSubmitService>().Verify(
            s => s.SubmitAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName =
        "INVARIANT: A curate:supervised caller can never reach the apply path that marks reports Processed, even when also holding curate, because only a supervisor may promote reports.")]
    [InlineData("curate:supervised")]
    [InlineData("curate", "curate:supervised")]
    [InlineData("curate:supervised", "curate", "admin")]
    public async Task supervised_caller_never_applies(params string[] permissions)
    {
        // Arrange
        _holdResult = new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.Conflict);
        var sut = _mocker.CreateInstance<PostDiscoveryCurationHandler>();

        // Act
        await sut.Handle(Context(_fixture.Create<string>(), permissions), new DiscoverySubmitRequest(),
            CancellationToken.None);

        // Assert
        _mocker.GetMock<IDiscoveryCurationSubmitService>().Verify(
            s => s.SubmitAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a supervised submit names a report that is not curatable, the handler returns 409, because the report is already held or processed.")]
    public async Task supervised_submit_conflict_returns_409()
    {
        // Arrange
        _holdResult = new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.Conflict,
            UnavailableDocumentIds: [_fixture.Create<Guid>()]);
        var sut = _mocker.CreateInstance<PostDiscoveryCurationHandler>();

        // Act
        var result = await sut.Handle(Context(_fixture.Create<string>(), "curate:supervised"),
            new DiscoverySubmitRequest(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName =
        "When a supervised submit names result ids outside its reports, the handler returns 400, because the request itself is invalid and nothing was held.")]
    public async Task supervised_submit_unknown_results_returns_400()
    {
        // Arrange
        _holdResult = new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.InvalidResultIds,
            UnknownResultIds: [_fixture.Create<Guid>()]);
        var sut = _mocker.CreateInstance<PostDiscoveryCurationHandler>();

        // Act
        var result = await sut.Handle(Context(_fixture.Create<string>(), "curate", "curate:supervised"),
            new DiscoverySubmitRequest(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName =
        "When an unsupervised curator submits, the submit is applied directly as before and never held.")]
    public async Task unsupervised_submit_is_applied()
    {
        // Arrange
        _submitResult = new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok,
            new DiscoverySubmitOutcome("Success", false, [], new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse()));
        var sut = _mocker.CreateInstance<PostDiscoveryCurationHandler>();

        // Act
        var result = await sut.Handle(Context(_fixture.Create<string>(), "curate"), new DiscoverySubmitRequest(),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IDiscoverySubmissionHoldService>().Verify(
            s => s.HoldAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When an unsupervised curator submits a report awaiting supervisor review, the handler returns 409, because held reports must not be curated by anyone else.")]
    public async Task unsupervised_submit_of_held_report_returns_409()
    {
        // Arrange
        _submitResult = new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Conflict);
        var sut = _mocker.CreateInstance<PostDiscoveryCurationHandler>();

        // Act
        var result = await sut.Handle(Context(_fixture.Create<string>(), "curate"), new DiscoverySubmitRequest(),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static IHandlerContext Context(string subject, params string[] permissions)
    {
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var principal = new ClientPrincipal
        {
            Claims = permissions.Select(p => new ClientPrincipalClaim { Type = "permissions", Value = p })
                .Append(new ClientPrincipalClaim { Type = ClaimTypes.NameIdentifier, Value = subject })
                .ToArray()
        };
        return new HandlerContext(req.Object, principal);
    }

    private static async Task<JsonElement> ReadJson(Microsoft.Azure.Functions.Worker.Http.HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return JsonDocument.Parse(await reader.ReadToEndAsync()).RootElement.Clone();
    }
}
