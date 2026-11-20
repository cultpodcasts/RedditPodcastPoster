using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.SubmitUrl;
using Api.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using FunctionHost.Tests.Api;

namespace FunctionHost.Tests.Api.Handlers;

public class PostSubmitUrlHandlerConflictTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private SubmitUrlResult _serviceResult = new(SubmitUrlStatus.Failed);

    public PostSubmitUrlHandlerConflictTests()
    {
        _mocker.GetMock<ISubmitUrlService>()
            .Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _serviceResult);
    }

    [Fact(DisplayName =
        "When submit holds on a service-link conflict, the handler returns 200 with serviceLinkConflict naming the " +
        "stored episode and each stored and submitted link, so the site can ask overwrite or create-new (#1416).")]
    public async Task service_link_conflict_returns_200_with_prompt()
    {
        // Arrange
        var episodeId = _fixture.CreateGuid();
        var title = _fixture.CreateTitle();
        var storedUrl = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var submittedUrl = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var hold = new ServiceLinkConflictHold(
            episodeId,
            title,
            DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay()),
            [new ServiceLinkConflictLink("spotify", storedUrl, submittedUrl)]);
        _serviceResult = new SubmitUrlResult(
            SubmitUrlStatus.ServiceLinkConflictDecisionRequired,
            new SubmitResult(SubmitResultState.None, SubmitResultState.None, ServiceLinkConflict: hold));
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = submittedUrl },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        var conflict = doc.RootElement.GetProperty("serviceLinkConflict");
        conflict.GetProperty("episodeId").GetGuid().Should().Be(episodeId);
        conflict.GetProperty("episodeTitle").GetString().Should().Be(title);
        var link = conflict.GetProperty("links").EnumerateArray().Should().ContainSingle().Subject;
        link.GetProperty("service").GetString().Should().Be("spotify");
        link.GetProperty("storedUrl").GetString().Should().Be(storedUrl.ToString());
        link.GetProperty("submittedUrl").GetString().Should().Be(submittedUrl.ToString());
        (doc.RootElement.TryGetProperty("success", out var success) && success.ValueKind != JsonValueKind.Null)
            .Should().BeFalse("nothing was written, so this is not a success");
    }

    [Fact(DisplayName = "PostSubmitUrlHandler maps Conflict to 409 with the ambiguous podcast id list.")]
    public async Task conflict_returns_409_uuid_array()
    {
        // Arrange
        var firstId = _fixture.CreateGuid();
        var secondId = _fixture.CreateGuid();
        _serviceResult = new SubmitUrlResult(SubmitUrlStatus.Conflict, AmbiguousPodcasts: [firstId, secondId]);
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        var ids = doc.RootElement.EnumerateArray().Select(e => e.GetGuid()).ToArray();
        ids.Should().BeEquivalentTo([firstId, secondId]);
    }

    [Fact(DisplayName =
        "When the submit service reports a missing podcast, the handler returns HTTP 404.")]
    public async Task podcast_not_found_returns_404()
    {
        // Arrange
        _serviceResult = new SubmitUrlResult(SubmitUrlStatus.PodcastNotFound, Message: _fixture.Create<string>());
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName =
        "When a TvShow name is ambiguous, the handler returns 409 with content kind TvShowEpisode, the parent name, and the parent ids.")]
    public async Task ambiguous_tv_show_returns_409_with_kind_and_parent_name()
    {
        // Arrange
        var parentName = _fixture.CreateTitle();
        var firstId = _fixture.CreateGuid();
        var secondId = _fixture.CreateGuid();
        _serviceResult = new SubmitUrlResult(
            SubmitUrlStatus.Conflict,
            AmbiguousPodcasts: [firstId, secondId],
            ContentKind: SubmitClassification.TvShowEpisode,
            ParentName: parentName);
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("contentKind").GetString().Should().Be(SubmitClassification.TvShowEpisode);
        doc.RootElement.GetProperty("parentName").GetString().Should().Be(parentName);
        var ids = doc.RootElement.GetProperty("parentIds").EnumerateArray().Select(e => e.GetGuid()).ToArray();
        ids.Should().BeEquivalentTo([firstId, secondId]);
    }

    [Fact(DisplayName =
        "When submit rejects a URL, the handler returns 400 with the content kind so the client can tell a reject from success.")]
    public async Task rejected_submit_returns_400_with_content_kind()
    {
        // Arrange
        _serviceResult = new SubmitUrlResult(
            SubmitUrlStatus.Rejected,
            new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: SubmitClassification.Episode,
                Rejected: true));
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("contentKind").GetString().Should().Be(SubmitClassification.Episode);
        doc.RootElement.GetProperty("rejected").GetBoolean().Should().BeTrue();
        doc.RootElement.TryGetProperty("message", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When submit rejects a BBC News page, the handler returns 400 with rejected true and the rejection message, " +
        "so the website can show why and where to submit instead (#1211 G5).")]
    public async Task rejected_bbc_news_submit_returns_400_with_message()
    {
        // Arrange
        _serviceResult = new SubmitUrlResult(
            SubmitUrlStatus.Rejected,
            new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: SubmitClassification.NewsReport,
                Rejected: true,
                RejectionMessage: BbcNewsSubmitRejection.Message));
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("rejected").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("contentKind").GetString().Should().Be(SubmitClassification.NewsReport);
        doc.RootElement.GetProperty("message").GetString().Should().Be(BbcNewsSubmitRejection.Message);
    }

    [Fact(DisplayName =
        "When submit needs a curator, the handler returns 422 with the content kind so the client can tell a hold from success.")]
    public async Task curator_hold_returns_422_with_content_kind()
    {
        // Arrange
        _serviceResult = new SubmitUrlResult(
            SubmitUrlStatus.RequiresCurator,
            new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: SubmitClassification.Episode,
                RequiresCurator: true));
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("contentKind").GetString().Should().Be(SubmitClassification.Episode);
        doc.RootElement.GetProperty("requiresCurator").GetBoolean().Should().BeTrue();
    }

    [Theory(DisplayName =
        "The handler lets only a caller with the curate scope resolve a service-link conflict; a submit-scope " +
        "caller is flagged as unable to, whatever the body says (#1416).")]
    [InlineData("curate", true)]
    [InlineData("submit", false)]
    public async Task only_curate_scope_resolves_service_link_conflicts(string scope, bool expected)
    {
        // Arrange
        SubmitUrlRequest? passed = null;
        _mocker.GetMock<ISubmitUrlService>()
            .Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .Callback((SubmitUrlRequest r, CancellationToken _) => passed = r)
            .ReturnsAsync(new SubmitUrlResult(SubmitUrlStatus.Failed));
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "permissions", Value = scope },
                new ClientPrincipalClaim { Type = ClaimTypes.NameIdentifier, Value = _fixture.CreateTitle() }
            ]
        };

        // Act
        await handler.Handle(
            new HandlerContext(req.Object, principal),
            new SubmitUrlRequest
            {
                Url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId()),
                ServiceLinkConflictDecision = "overwrite"
            },
            CancellationToken.None);

        // Assert
        passed!.CanResolveServiceLinkConflict.Should().Be(expected);
    }
}
