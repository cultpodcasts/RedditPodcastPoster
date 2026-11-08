using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Handlers.Films;
using Api.Handlers.News;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.TvShows;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.Films;
using Api.Services.News;
using Api.Services.People;
using Api.Services.Podcasts;
using Api.Services.Public;
using Api.Services.Subjects;
using Api.Services.SubmitUrl;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using Person = RedditPodcastPoster.Models.People.Person;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// Thin-handler status-to-HTTP mapping matrix. Handlers are Controller -> Handler -> Service ->
/// Models.*Result; handlers switch on the service Status enum and call ToDto() on success. These
/// tests mock the service one layer below the handler and assert only on the HTTP outcome (status
/// code, and — for failure paths — the JSON "error" body), so they stay valid whether the failure
/// DTO is Api.Dtos.SubmitUrlResponse or Api.Dtos.ApiErrorResponse.
/// </summary>
public class HandlerStatusMatrixTests
{
    private readonly DomainTestFixture _fixture = new();

    private static async Task<JsonElement> ReadJsonBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ----- GetPersonHandler -----

    [Fact(DisplayName = "GetPersonHandler maps Ok to 200")]
    public async Task GetPersonHandler_ok_returns_200()
    {
        // Arrange
        var mocker = new AutoMocker();
        var person = new Person(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        mocker.GetMock<IPersonGetService>()
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.Ok, person));
        mocker.Use(NullLogger<GetPersonHandler>.Instance);
        var handler = mocker.CreateInstance<GetPersonHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), person.Name, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetPersonHandler maps NotFound to 404")]
    public async Task GetPersonHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IPersonGetService>()
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.NotFound));
        mocker.Use(NullLogger<GetPersonHandler>.Instance);
        var handler = mocker.CreateInstance<GetPersonHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GetPersonHandler maps Failed to 500 with error JSON body")]
    public async Task GetPersonHandler_failed_returns_500_with_error_body()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IPersonGetService>()
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.Failed));
        mocker.Use(NullLogger<GetPersonHandler>.Instance);
        var handler = mocker.CreateInstance<GetPersonHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ----- GetPodcastHandler -----

    [Fact(DisplayName = "GetPodcastHandler maps Found to 200")]
    public async Task GetPodcastHandler_found_returns_200()
    {
        // Arrange
        var mocker = new AutoMocker();
        var podcast = new Podcast { Id = _fixture.CreateGuid(), Name = _fixture.CreateTitle() };
        mocker.GetMock<IPodcastGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.Found, podcast));
        mocker.Use(NullLogger<GetPodcastHandler>.Instance);
        var handler = mocker.CreateInstance<GetPodcastHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), new PodcastGetRequest(podcast.Id), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetPodcastHandler maps NotFound to 404 with error JSON body")]
    public async Task GetPodcastHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IPodcastGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.NotFound));
        mocker.Use(NullLogger<GetPodcastHandler>.Instance);
        var handler = mocker.CreateInstance<GetPodcastHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastGetRequest(_fixture.CreateGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "GetPodcastHandler maps Conflict with ambiguous podcasts to 409 with candidate list body")]
    public async Task GetPodcastHandler_conflict_with_ambiguous_returns_409()
    {
        // Arrange
        var mocker = new AutoMocker();
        var ambiguous = new[] { _fixture.CreateGuid(), _fixture.CreateGuid() };
        mocker.GetMock<IPodcastGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.Conflict, AmbiguousPodcasts: ambiguous));
        mocker.Use(NullLogger<GetPodcastHandler>.Instance);
        var handler = mocker.CreateInstance<GetPodcastHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastGetRequest(_fixture.CreateTitle(), null),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonBodyAsync(result);
        body.GetArrayLength().Should().Be(ambiguous.Length);
    }

    [Fact(DisplayName = "GetPodcastHandler maps Failed to 500 with error JSON body")]
    public async Task GetPodcastHandler_failed_returns_500_with_error_body()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IPodcastGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.Failed));
        mocker.Use(NullLogger<GetPodcastHandler>.Instance);
        var handler = mocker.CreateInstance<GetPodcastHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastGetRequest(_fixture.CreateGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ----- GetSubjectHandler -----

    [Fact(DisplayName = "GetSubjectHandler maps Ok to 200")]
    public async Task GetSubjectHandler_ok_returns_200()
    {
        // Arrange
        var mocker = new AutoMocker();
        var subjectName = _fixture.CreateTitle();
        var subject = new Subject(subjectName);
        mocker.GetMock<ISubjectGetService>()
            .Setup(s => s.GetAsync(subjectName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectGetResult(SubjectGetStatus.Ok, subject));
        mocker.Use(NullLogger<GetSubjectHandler>.Instance);
        var handler = mocker.CreateInstance<GetSubjectHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), subjectName, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetSubjectHandler maps NotFound to 404")]
    public async Task GetSubjectHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<ISubjectGetService>()
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectGetResult(SubjectGetStatus.NotFound));
        mocker.Use(NullLogger<GetSubjectHandler>.Instance);
        var handler = mocker.CreateInstance<GetSubjectHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GetSubjectHandler maps Failed to 500 with error JSON body")]
    public async Task GetSubjectHandler_failed_returns_500_with_error_body()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<ISubjectGetService>()
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectGetResult(SubjectGetStatus.Failed));
        mocker.Use(NullLogger<GetSubjectHandler>.Instance);
        var handler = mocker.CreateInstance<GetSubjectHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ----- DeleteEpisodeHandler -----

    [Fact(DisplayName = "DeleteEpisodeHandler maps Deleted to 200")]
    public async Task DeleteEpisodeHandler_deleted_returns_200()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IEpisodeDeleteService>()
            .Setup(s => s.DeleteAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted));
        mocker.Use(NullLogger<DeleteEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");
        var wrapper = new PodcastEpisodeRequestWrapper(_fixture.CreateGuid(), _fixture.CreateGuid());

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), wrapper, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DeleteEpisodeHandler maps NotFound to 404")]
    public async Task DeleteEpisodeHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IEpisodeDeleteService>()
            .Setup(s => s.DeleteAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound));
        mocker.Use(NullLogger<DeleteEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(_fixture.CreateGuid(), _fixture.CreateGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DeleteEpisodeHandler maps AlreadySocial to 400 with tweeted body")]
    public async Task DeleteEpisodeHandler_already_social_returns_400()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IEpisodeDeleteService>()
            .Setup(s => s.DeleteAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.AlreadySocial, Posted: false, Tweeted: true));
        mocker.Use(NullLogger<DeleteEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(_fixture.CreateGuid(), _fixture.CreateGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("tweeted").GetBoolean().Should().BeTrue();
    }

    // ----- DeleteFilmHandler -----

    [Fact(DisplayName = "DeleteFilmHandler maps Deleted to 200")]
    public async Task DeleteFilmHandler_deleted_returns_200()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IFilmService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted));
        mocker.Use(NullLogger<DeleteFilmHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteFilmHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DeleteFilmHandler maps NotFound to 404")]
    public async Task DeleteFilmHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IFilmService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound));
        mocker.Use(NullLogger<DeleteFilmHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteFilmHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DeleteFilmHandler maps AlreadySocial to 400 with tweeted body")]
    public async Task DeleteFilmHandler_already_social_returns_400()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IFilmService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.AlreadySocial, Posted: false, Tweeted: true));
        mocker.Use(NullLogger<DeleteFilmHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteFilmHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("tweeted").GetBoolean().Should().BeTrue();
    }

    // ----- DeleteNewsReportHandler -----

    [Fact(DisplayName = "DeleteNewsReportHandler maps Deleted to 200")]
    public async Task DeleteNewsReportHandler_deleted_returns_200()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<INewsReportService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted));
        mocker.Use(NullLogger<DeleteNewsReportHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteNewsReportHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DeleteNewsReportHandler maps NotFound to 404")]
    public async Task DeleteNewsReportHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<INewsReportService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound));
        mocker.Use(NullLogger<DeleteNewsReportHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteNewsReportHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DeleteNewsReportHandler maps AlreadySocial to 400 with tweeted body")]
    public async Task DeleteNewsReportHandler_already_social_returns_400()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<INewsReportService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.AlreadySocial, Posted: false, Tweeted: true));
        mocker.Use(NullLogger<DeleteNewsReportHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteNewsReportHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("tweeted").GetBoolean().Should().BeTrue();
    }

    // ----- DeleteTvShowEpisodeHandler -----

    [Fact(DisplayName = "DeleteTvShowEpisodeHandler maps Deleted to 200")]
    public async Task DeleteTvShowEpisodeHandler_deleted_returns_200()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<ITvShowEpisodeService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted));
        mocker.Use(NullLogger<DeleteTvShowEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteTvShowEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DeleteTvShowEpisodeHandler maps NotFound to 404")]
    public async Task DeleteTvShowEpisodeHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<ITvShowEpisodeService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound));
        mocker.Use(NullLogger<DeleteTvShowEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteTvShowEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DeleteTvShowEpisodeHandler maps AlreadySocial to 400 with tweeted body")]
    public async Task DeleteTvShowEpisodeHandler_already_social_returns_400()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<ITvShowEpisodeService>()
            .Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.AlreadySocial, Posted: false, Tweeted: true));
        mocker.Use(NullLogger<DeleteTvShowEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<DeleteTvShowEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("tweeted").GetBoolean().Should().BeTrue();
    }

    // ----- PostSubmitUrlHandler -----

    [Fact(DisplayName = "PostSubmitUrlHandler maps PodcastNotFound to 404")]
    public async Task PostSubmitUrlHandler_podcast_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<ISubmitUrlService>()
            .Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlResult(SubmitUrlStatus.PodcastNotFound, Message: _fixture.CreateTitle()));
        mocker.Use(NullLogger<PostSubmitUrlHandler>.Instance);
        var handler = mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri("https://example.com/ep") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PostSubmitUrlHandler maps Failed to 500")]
    public async Task PostSubmitUrlHandler_failed_returns_500()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<ISubmitUrlService>()
            .Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlResult(SubmitUrlStatus.Failed, Message: _fixture.CreateTitle()));
        mocker.Use(NullLogger<PostSubmitUrlHandler>.Instance);
        var handler = mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri("https://example.com/ep") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ----- GetPublicEpisodeHandler -----

    [Fact(DisplayName = "GetPublicEpisodeHandler maps NotFound to 404")]
    public async Task GetPublicEpisodeHandler_not_found_returns_404()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IPublicEpisodeGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicEpisodeGetResult(PublicEpisodeGetStatus.NotFound));
        mocker.Use(NullLogger<GetPublicEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<GetPublicEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(_fixture.CreateGuid(), _fixture.CreateGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GetPublicEpisodeHandler maps Failed to 500 with error JSON body")]
    public async Task GetPublicEpisodeHandler_failed_returns_500_with_error_body()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<IPublicEpisodeGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicEpisodeGetResult(PublicEpisodeGetStatus.Failed));
        mocker.Use(NullLogger<GetPublicEpisodeHandler>.Instance);
        var handler = mocker.CreateInstance<GetPublicEpisodeHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(_fixture.CreateGuid(), _fixture.CreateGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
