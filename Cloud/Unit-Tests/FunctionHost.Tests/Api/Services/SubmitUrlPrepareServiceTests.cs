using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class SubmitUrlPrepareServiceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<INonPodcastServiceAdapter> _adapter = new();
    private INonPodcastServiceAdapter? _resolvedAdapter;
    private NonPodcastServiceItemMetaData? _liveMeta;
    private Exception? _liveExtractException;
    private Exception? _htmlExtractException;

    public SubmitUrlPrepareServiceTests()
    {
        _mocker.GetMock<INonPodcastServiceAdapterResolver>()
            .Setup(r => r.ForExtract(It.IsAny<Uri>()))
            .Returns(() => _resolvedAdapter);

        _adapter.Setup(a => a.Service).Returns(NonPodcastService.Itvx);
        _adapter.Setup(a => a.ExtractMetaData(It.IsAny<Uri>()))
            .Returns((Uri _) =>
            {
                if (_liveExtractException is not null)
                {
                    return Task.FromException<NonPodcastServiceItemMetaData>(_liveExtractException);
                }

                return Task.FromResult(_liveMeta!);
            });
        _adapter.Setup(a => a.ExtractMetaData(It.IsAny<Uri>(), It.IsAny<string>()))
            .Returns((Uri _, string __) =>
            {
                if (_htmlExtractException is not null)
                {
                    return Task.FromException<NonPodcastServiceItemMetaData>(_htmlExtractException);
                }

                return Task.FromResult(_liveMeta!);
            });
    }

    [Fact(DisplayName =
        "When no extract adapter resolves the URL, prepare returns BadRequest because the destination is unsupported.")]
    public async Task unsupported_url_prepare_returns_bad_request()
    {
        // Arrange
        _resolvedAdapter = null;
        var url = new Uri($"https://example.com/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<SubmitUrlPrepareService>();

        // Act
        var result = await sut.PrepareAsync(url, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlPrepareStatus.BadRequest);
        result.Message.Should().Be("Url is not a supported streaming extract destination");
        result.Response.Should().BeNull();
        _adapter.Verify(a => a.ExtractMetaData(It.IsAny<Uri>()), Times.Never);
        _adapter.Verify(a => a.ExtractMetaData(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "When HTML extract throws NotSupportedException, extract returns BadRequest with that message " +
        "because the adapter has no HTML path for the destination.")]
    public async Task html_extract_not_supported_returns_bad_request()
    {
        // Arrange
        var url = ItvxUrl();
        var html = _fixture.Create<string>();
        var message = _fixture.Create<string>();
        _resolvedAdapter = _adapter.Object;
        _htmlExtractException = new NotSupportedException(message);
        var sut = _mocker.CreateInstance<SubmitUrlPrepareService>();

        // Act
        var result = await sut.ExtractAsync(url, html, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlPrepareStatus.BadRequest);
        result.Message.Should().Be(message);
        result.Response.Should().BeNull();
        _adapter.Verify(a => a.ExtractMetaData(url, html), Times.Once);
        _adapter.Verify(a => a.ExtractMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When live prepare succeeds, the service returns Ok with SubmitUrlPrepareResponse.From fields " +
        "so Worker can cache service and series name without a second scrape.")]
    public async Task prepare_ok_returns_from_response_fields()
    {
        // Arrange
        var url = ItvxUrl();
        var title = _fixture.CreateTitle();
        var showName = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        _liveMeta = new NonPodcastServiceItemMetaData(
            Title: title,
            Description: description,
            ShowName: showName);
        _resolvedAdapter = _adapter.Object;
        var sut = _mocker.CreateInstance<SubmitUrlPrepareService>();

        // Act
        var result = await sut.PrepareAsync(url, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlPrepareStatus.Ok);
        result.Response.Should().BeEquivalentTo(
            SubmitUrlPrepareResponse.From(url, _liveMeta, NonPodcastService.Itvx));
        result.Response!.Service.Should().Be(ServiceKeys.Itvx);
        result.Response.Title.Should().Be(title);
        result.Response.ShowName.Should().Be(showName);
        result.Response.PodcastName.Should().Be(showName);
        _adapter.Verify(a => a.ExtractMetaData(url), Times.Once);
        _adapter.Verify(a => a.ExtractMetaData(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "When HTML extract succeeds, the service returns Ok with SubmitUrlPrepareResponse.From fields " +
        "so Browser Rendering HTML maps without a second Azure live fetch.")]
    public async Task extract_ok_returns_from_response_fields()
    {
        // Arrange
        var url = ItvxUrl();
        var html = _fixture.Create<string>();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        _liveMeta = new NonPodcastServiceItemMetaData(
            Title: title,
            Description: description);
        _resolvedAdapter = _adapter.Object;
        var sut = _mocker.CreateInstance<SubmitUrlPrepareService>();

        // Act
        var result = await sut.ExtractAsync(url, html, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlPrepareStatus.Ok);
        result.Response.Should().BeEquivalentTo(
            SubmitUrlPrepareResponse.From(url, _liveMeta, NonPodcastService.Itvx));
        result.Response!.Service.Should().Be(ServiceKeys.Itvx);
        result.Response.Title.Should().Be(title);
        _adapter.Verify(a => a.ExtractMetaData(url, html), Times.Once);
        _adapter.Verify(a => a.ExtractMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the adapter throws a generic exception, prepare returns Failed so the handler can map 500 without leaking details.")]
    public async Task adapter_generic_failure_returns_failed()
    {
        // Arrange
        var url = ItvxUrl();
        _resolvedAdapter = _adapter.Object;
        _liveExtractException = new InvalidOperationException(_fixture.Create<string>());
        var sut = _mocker.CreateInstance<SubmitUrlPrepareService>();

        // Act
        var result = await sut.PrepareAsync(url, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlPrepareStatus.Failed);
        result.Message.Should().Be("Failure");
        result.Response.Should().BeNull();
    }

    private Uri ItvxUrl() =>
        new($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
}
