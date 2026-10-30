using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.News;
using Api.Handlers.TvShows;
using Api.Models;
using Api.Services.News;
using Api.Services.TvShows;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// The news-organisation and TV-show edit dialogs (website#575) read <c>message</c> and <c>code</c> from a refused
/// edit, and <c>retryable</c> from a 503, so the body shape is pinned here.
/// </summary>
public class PostPublisherHandlerPlaylistOwnershipTests
{
    private const string MismatchCode = "youTubePlaylistChannelMismatch";
    private const string LookupFailedCode = "youTubePlaylistOwnerLookupFailed";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static HandlerContext Context()
    {
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        return new HandlerContext(req.Object, new ClientPrincipal());
    }

    private static async Task<JsonElement> Body(Microsoft.Azure.Functions.Worker.Http.HttpResponseData response)
    {
        response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Body);
        return document.RootElement.Clone();
    }

    private void NewsReturns(NewsOrganisationUpdateResult result) =>
        _mocker.GetMock<INewsOrganisationService>()
            .Setup(s => s.UpdateAsync(It.IsAny<NewsOrganisationChangeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void TvReturns(TvShowUpdateResult result) =>
        _mocker.GetMock<ITvShowService>()
            .Setup(s => s.UpdateAsync(It.IsAny<TvShowChangeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact(DisplayName = "POST news organisation: an ownership refusal is a 400 with message and code.")]
    public async Task news_ownership_bad_request_has_message_and_code()
    {
        // Arrange
        NewsReturns(new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.BadRequest, "mismatch", Code: MismatchCode));
        var sut = _mocker.CreateInstance<PostNewsOrganisationHandler>();

        // Act
        var response = await sut.Handle(Context(),
            new NewsOrganisationChangeRequestWrapper(_fixture.CreateGuid(), new NewsOrganisationChangeRequest()),
            CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await Body(response);
        body.GetProperty("message").GetString().Should().Be("mismatch");
        body.GetProperty("code").GetString().Should().Be(MismatchCode);
    }

    [Fact(DisplayName = "POST news organisation: a failed owner lookup is a 503 with message, code and retryable=true.")]
    public async Task news_lookup_failure_is_retryable_503()
    {
        // Arrange
        NewsReturns(new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.ServiceUnavailable, "try again",
            Code: LookupFailedCode));
        var sut = _mocker.CreateInstance<PostNewsOrganisationHandler>();

        // Act
        var response = await sut.Handle(Context(),
            new NewsOrganisationChangeRequestWrapper(_fixture.CreateGuid(), new NewsOrganisationChangeRequest()),
            CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await Body(response);
        body.GetProperty("message").GetString().Should().Be("try again");
        body.GetProperty("code").GetString().Should().Be(LookupFailedCode);
        body.GetProperty("retryable").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "POST TV show: an ownership refusal is a 400 with message and code.")]
    public async Task tv_ownership_bad_request_has_message_and_code()
    {
        // Arrange
        TvReturns(new TvShowUpdateResult(TvShowUpdateStatus.BadRequest, "mismatch", Code: MismatchCode));
        var sut = _mocker.CreateInstance<PostTvShowHandler>();

        // Act
        var response = await sut.Handle(Context(),
            new TvShowChangeRequestWrapper(_fixture.CreateGuid(), new TvShowChangeRequest()),
            CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await Body(response);
        body.GetProperty("message").GetString().Should().Be("mismatch");
        body.GetProperty("code").GetString().Should().Be(MismatchCode);
    }

    [Fact(DisplayName = "POST TV show: a failed owner lookup is a 503 with message, code and retryable=true.")]
    public async Task tv_lookup_failure_is_retryable_503()
    {
        // Arrange
        TvReturns(new TvShowUpdateResult(TvShowUpdateStatus.ServiceUnavailable, "try again", Code: LookupFailedCode));
        var sut = _mocker.CreateInstance<PostTvShowHandler>();

        // Act
        var response = await sut.Handle(Context(),
            new TvShowChangeRequestWrapper(_fixture.CreateGuid(), new TvShowChangeRequest()),
            CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await Body(response);
        body.GetProperty("code").GetString().Should().Be(LookupFailedCode);
        body.GetProperty("retryable").GetBoolean().Should().BeTrue();
    }
}
