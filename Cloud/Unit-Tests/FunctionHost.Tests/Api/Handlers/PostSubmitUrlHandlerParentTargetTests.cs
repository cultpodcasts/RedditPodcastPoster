using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.SubmitUrl;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using FunctionHost.Tests.Api;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// HTTP mapping of the website hub's parent-target submit results (website #530): a missing parent is 404 and an
/// invalid target is 400.
/// </summary>
public class PostSubmitUrlHandlerParentTargetTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private SubmitUrlResult _serviceResult = new(SubmitUrlStatus.Failed);

    public PostSubmitUrlHandlerParentTargetTests()
    {
        _mocker.GetMock<ISubmitUrlService>()
            .Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _serviceResult);
    }

    private Task<HttpResponseData> Handle()
    {
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        return _mocker.CreateInstance<PostSubmitUrlHandler>().Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest
            {
                Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
                ParentKind = "NewsOrganisation", ParentId = _fixture.CreateGuid()
            },
            CancellationToken.None);
    }

    [Fact(DisplayName = "PostSubmitUrlHandler maps ParentNotFound to 404.")]
    public async Task parent_not_found_returns_404()
    {
        // Arrange
        _serviceResult = new SubmitUrlResult(SubmitUrlStatus.ParentNotFound, Message: _fixture.CreateTitle());

        // Act
        var response = await Handle();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PostSubmitUrlHandler maps BadRequest to 400.")]
    public async Task bad_request_returns_400()
    {
        // Arrange
        _serviceResult = new SubmitUrlResult(SubmitUrlStatus.BadRequest, Message: _fixture.CreateTitle());

        // Act
        var response = await Handle();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
